using NanoSanjabu.Models;
using System;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace NanoSanjabu.Services
{
    public class MesRuntimeService : IDisposable
    {
        private PlcService? _plcService;
        private readonly MesRepository _repository;
        private readonly Timer _timer;

        private PlcData? _previousData;
        private bool _isProcessing;

        public bool IsPlcConnected => _plcService != null && _plcService.IsConnected;
        public long CurrentTrayRunId { get; private set; }

        public MesSnapshot CurrentSnapshot { get; private set; } = new MesSnapshot
        {
            InputSlots = MesUiFactory.CreateDefaultInputSlots(),
            StackGroups = MesUiFactory.CreateDefaultStackGroups(),
            Reports = new()
        };

        public event EventHandler? SnapshotUpdated;

        public MesRuntimeService(MesRepository repository)
        {
            _repository = repository;

            _timer = new Timer(1000);
            _timer.Elapsed += async (_, _) => await OnTickAsync();
        }

        public void SetPlcService(PlcService plcService)
        {
            _plcService = plcService;
        }

        public async Task InitializeSnapshotAsync()
        {
            try
            {
                CurrentTrayRunId = await _repository.GetOrCreateActiveTrayRunAsync();
                await RefreshSnapshotAsync();
            }
            catch
            {
                CurrentSnapshot = new MesSnapshot
                {
                    Dashboard = new DashboardSummary(),
                    History = new HistorySummary(),
                    InputSlots = MesUiFactory.CreateDefaultInputSlots(),
                    StackGroups = MesUiFactory.CreateDefaultStackGroups(),
                    Reports = new(),
                    CurrentTrayLotNo = "대기 중"
                };

                SnapshotUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task StartPlcAsync()
        {
            if (_plcService == null)
                throw new InvalidOperationException("PLC 서비스가 설정되지 않았습니다.");

            if (!_plcService.IsConnected)
            {
                bool connected = _plcService.Connect(out int errorCode);
                if (!connected)
                    throw new InvalidOperationException($"PLC 연결 실패: ErrorCode={errorCode}");
            }

            if (CurrentTrayRunId == 0)
                CurrentTrayRunId = await _repository.GetOrCreateActiveTrayRunAsync();

            _timer.Start();
        }

        private async Task OnTickAsync()
        {
            if (_isProcessing)
                return;

            if (_plcService == null || !_plcService.IsConnected)
                return;

            _isProcessing = true;

            try
            {
                PlcData current = _plcService.ReadAll();
                await ProcessPlcAsync(current);
                _previousData = current;
                await RefreshSnapshotAsync();
            }
            catch
            {
                _timer.Stop();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task ProcessPlcAsync(PlcData current)
        {
            short loaderX = current.PositionIndex[0]; // D60
            short loaderY = current.PositionIndex[1]; // D61

            // 1) 투입부 현재 작업 위치 -> LOADING
            if (PlcAddressMapper.TryGetInputSlot(loaderX, loaderY, out _, out _, out int currentSlotNo))
            {
                await _repository.MarkSlotLoadingAsync(CurrentTrayRunId, currentSlotNo);
            }

            // 2) Glass 안착 완료
            if (IsRisingEdge(_previousData?.M858_GlassLoaded, current.M858_GlassLoaded) &&
                PlcAddressMapper.TryGetInputSlot(loaderX, loaderY, out _, out _, out int glassSlotNo))
            {
                await _repository.MarkGlassLoadedAsync(CurrentTrayRunId, glassSlotNo);
            }

            // 3) Nano 완료 -> COMPLETE
            if (IsRisingEdge(_previousData?.M863_NanoDone, current.M863_NanoDone) &&
                PlcAddressMapper.TryGetInputSlot(loaderX, loaderY, out _, out _, out int nanoSlotNo))
            {
                await _repository.MarkNanoDoneAsync(CurrentTrayRunId, nanoSlotNo);
            }

            // 4) Dry Zone
            if (IsRisingEdge(_previousData?.L1_DryStartUpper, current.L1_DryStartUpper))
            {
                await _repository.MarkDryStartedAsync(CurrentTrayRunId, "UPPER_DRY");
            }

            if (IsRisingEdge(_previousData?.L2_DryStartLower, current.L2_DryStartLower))
            {
                await _repository.MarkDryStartedAsync(CurrentTrayRunId, "LOWER_DRY");
            }

            if (IsRisingEdge(_previousData?.L3_DryEndUpper, current.L3_DryEndUpper))
            {
                await _repository.MarkDryCompletedAsync(CurrentTrayRunId, "UPPER_DRY");
            }

            if (IsRisingEdge(_previousData?.L4_DryEndLower, current.L4_DryEndLower))
            {
                await _repository.MarkDryCompletedAsync(CurrentTrayRunId, "LOWER_DRY");
            }

            // 5) 적층 진행 그룹
            int currentGroupNo = PlcAddressMapper.GetCurrentStackGroup(current.D26_StackOutCount, current.D20_StackInput);

            if (current.D20_StackInput > 0)
            {
                await _repository.MarkStackGroupLoadingAsync(CurrentTrayRunId, currentGroupNo);
            }

            // 6) 적층 source slot 수집
            // D65 = Transfer X(열 1~10), D66 = Unloader X(행 1~5), D67 = Unloader Y(Tray 작업 위치=1)
            short transferX = current.PositionIndex[5]; // D65
            short unloaderX = current.PositionIndex[6]; // D66
            short unloaderY = current.PositionIndex[7]; // D67

            if (current.D20_StackInput >= 1 && current.D20_StackInput <= 5 &&
                PlcAddressMapper.TryGetStackPickSlot(transferX, unloaderX, unloaderY, out _, out _, out int pickedSlotNo))
            {
                await _repository.UpsertStackGroupItemAsync(
                    CurrentTrayRunId,
                    currentGroupNo,
                    pickedSlotNo,
                    current.D20_StackInput);
            }

            // 7) UV 시작
            if (IsRisingEdge(_previousData?.M922_UVRun, current.M922_UVRun))
            {
                await _repository.MarkUvRunningAsync(CurrentTrayRunId, currentGroupNo);
            }

            // 8) 적층 완료
            if (IsRisingEdge(_previousData?.M937_StackOut, current.M937_StackOut))
            {
                int completedGroupNo = current.D26_StackOutCount;
                if (completedGroupNo < 1)
                {
                    completedGroupNo = currentGroupNo;
                }

                // D66=Unloader X, D67=Unloader Y
                int outRowNo;
                int outColNo;

                if (!PlcAddressMapper.TryGetStackOutPosition(unloaderX, unloaderY, out outRowNo, out outColNo))
                {
                    PlcAddressMapper.GetDefaultStackOutPosition(completedGroupNo, out outRowNo, out outColNo);
                }

                await _repository.MarkStackGroupCompletedAsync(CurrentTrayRunId, completedGroupNo, outRowNo, outColNo);

                bool trayCompleted = await _repository.CompleteTrayIfFinishedAsync(CurrentTrayRunId);
                if (trayCompleted)
                {
                    CurrentTrayRunId = await _repository.GetOrCreateActiveTrayRunAsync();
                }
            }

            // 9) 알람
            bool d0Changed = _previousData == null || _previousData.D0_Error != current.D0_Error;
            if (d0Changed)
            {
                await _repository.HandleAlarmAsync(CurrentTrayRunId, current.D0_Error);
            }
        }

        public async Task RefreshSnapshotAsync()
        {
            try
            {
                if (CurrentTrayRunId == 0)
                {
                    CurrentTrayRunId = await _repository.GetOrCreateActiveTrayRunAsync();
                }

                var inputSlots = await _repository.GetInputSlotsAsync(CurrentTrayRunId);
                var stackGroups = await _repository.GetStackGroupsAsync(CurrentTrayRunId);

                CurrentSnapshot = new MesSnapshot
                {
                    Dashboard = await _repository.GetDashboardSummaryAsync(),
                    History = await _repository.GetHistorySummaryAsync(),
                    InputSlots = inputSlots.Count > 0 ? inputSlots : MesUiFactory.CreateDefaultInputSlots(),
                    StackGroups = stackGroups.Count > 0 ? stackGroups : MesUiFactory.CreateDefaultStackGroups(),
                    Reports = await _repository.GetRecentReportsAsync(20),
                    CurrentTrayLotNo = await _repository.GetTrayLotNoAsync(CurrentTrayRunId)
                };

                if (string.IsNullOrWhiteSpace(CurrentSnapshot.CurrentTrayLotNo))
                {
                    CurrentSnapshot.CurrentTrayLotNo = "대기 중";
                }
            }
            catch
            {
                CurrentSnapshot = new MesSnapshot
                {
                    Dashboard = new DashboardSummary(),
                    History = new HistorySummary(),
                    InputSlots = MesUiFactory.CreateDefaultInputSlots(),
                    StackGroups = MesUiFactory.CreateDefaultStackGroups(),
                    Reports = new(),
                    CurrentTrayLotNo = "대기 중"
                };
            }

            SnapshotUpdated?.Invoke(this, EventArgs.Empty);
        }

        private static bool IsRisingEdge(bool? previous, bool current)
        {
            return previous.HasValue && previous.Value == false && current;
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            _plcService?.Dispose();
        }
    }
}