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
            {
                throw new InvalidOperationException("PLC 서비스가 설정되지 않았습니다.");
            }

            if (!_plcService.IsConnected)
            {
                bool connected = _plcService.Connect(out int errorCode);
                if (!connected)
                {
                    throw new InvalidOperationException($"PLC 연결 실패: ErrorCode={errorCode}");
                }
            }

            if (CurrentTrayRunId == 0)
            {
                CurrentTrayRunId = await _repository.GetOrCreateActiveTrayRunAsync();
            }

            _timer.Start();
        }

        private async Task OnTickAsync()
        {
            if (_isProcessing)
            {
                return;
            }

            if (_plcService == null || !_plcService.IsConnected)
            {
                return;
            }

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
            if (PlcAddressMapper.TryGetInputSlot(
                current.PositionIndex[0],
                current.PositionIndex[1],
                out _,
                out _,
                out int currentSlotNo))
            {
                await _repository.MarkSlotRunningAsync(CurrentTrayRunId, currentSlotNo);
            }

            if (IsRisingEdge(_previousData?.M858_GlassLoaded, current.M858_GlassLoaded) &&
                PlcAddressMapper.TryGetInputSlot(current.PositionIndex[0], current.PositionIndex[1], out _, out _, out int glassSlotNo))
            {
                await _repository.MarkGlassLoadedAsync(CurrentTrayRunId, glassSlotNo);
            }

            if (IsRisingEdge(_previousData?.M863_NanoDone, current.M863_NanoDone) &&
                PlcAddressMapper.TryGetInputSlot(current.PositionIndex[0], current.PositionIndex[1], out _, out _, out int nanoSlotNo))
            {
                await _repository.MarkNanoDoneAsync(CurrentTrayRunId, nanoSlotNo);
            }

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

            int currentGroupNo = PlcAddressMapper.GetCurrentStackGroup(current.D26_StackOutCount, current.D20_StackInput);
            await _repository.MarkStackGroupRunningAsync(CurrentTrayRunId, currentGroupNo);

            if (IsRisingEdge(_previousData?.M922_UVRun, current.M922_UVRun))
            {
                await _repository.MarkUvRunningAsync(CurrentTrayRunId, currentGroupNo);
            }

            if (IsRisingEdge(_previousData?.M937_StackOut, current.M937_StackOut))
            {
                int completedGroupNo = current.D26_StackOutCount;
                if (completedGroupNo < 1)
                {
                    completedGroupNo = currentGroupNo;
                }

                await _repository.MarkStackGroupCompletedAsync(CurrentTrayRunId, completedGroupNo);

                bool trayCompleted = await _repository.CompleteTrayIfFinishedAsync(CurrentTrayRunId);
                if (trayCompleted)
                {
                    CurrentTrayRunId = await _repository.GetOrCreateActiveTrayRunAsync();
                }
            }

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