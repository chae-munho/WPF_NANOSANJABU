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
            StackBoardCells = MesUiFactory.CreateDefaultStackBoardCells(),
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
                CurrentTrayRunId = await _repository.GetOrCreateCurrentTrayRunAsync();
                await RefreshSnapshotAsync();
            }
            catch
            {
                CurrentSnapshot = new MesSnapshot
                {
                    Dashboard = new DashboardSummary(),
                    History = new HistorySummary(),
                    InputSlots = MesUiFactory.CreateDefaultInputSlots(),
                    StackBoardCells = MesUiFactory.CreateDefaultStackBoardCells(),
                    Reports = new(),
                    CurrentTrayRunNo = "대기 중",
                    CurrentTrayTypeText = ""
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
                CurrentTrayRunId = await _repository.GetOrCreateCurrentTrayRunAsync();

            _timer.Start();
        }

        private async Task OnTickAsync()
        {
            if (_isProcessing)
                return;

            if (_plcService == null)
                return;

            _isProcessing = true;

            try
            {
                if (!_plcService.IsConnected)
                {
                    bool reconnected = _plcService.Connect(out int reconnectErrorCode);
                    if (!reconnected)
                    {
                        await SafeAddRuntimeErrorReportAsync(
                            $"PLC 재연결 실패: ErrorCode={reconnectErrorCode}");
                        return;
                    }

                    await SafeAddRuntimeErrorReportAsync("PLC 재연결 성공");
                }

                PlcData current = _plcService.ReadAll();
                await ProcessPlcAsync(current);
                _previousData = current;
                await RefreshSnapshotAsync();
            }
            catch (Exception ex)
            {
                await SafeAddRuntimeErrorReportAsync($"런타임 처리 오류: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task ProcessPlcAsync(PlcData current)
        {
            if (CurrentTrayRunId == 0)
                CurrentTrayRunId = await _repository.GetOrCreateCurrentTrayRunAsync();

            // D60, D61 -> 투입부 현재 슬롯
            short loaderX = current.PositionIndex[0]; // D60
            short loaderY = current.PositionIndex[1]; // D61

            if (PlcAddressMapper.TryGetInputSlot(loaderX, loaderY, out _, out _, out int currentSlotNo))
            {
                await _repository.MarkSlotLoadingAsync(CurrentTrayRunId, currentSlotNo);
            }

            // M858 상승엣지 -> Glass 안착 완료
            if (IsRisingEdge(_previousData?.M858_GlassLoaded, current.M858_GlassLoaded) &&
                PlcAddressMapper.TryGetInputSlot(loaderX, loaderY, out _, out _, out int glassSlotNo))
            {
                await _repository.MarkGlassLoadedAsync(CurrentTrayRunId, glassSlotNo);
            }

            // M863 상승엣지 -> Nano 완료
            if (IsRisingEdge(_previousData?.M863_NanoDone, current.M863_NanoDone) &&
                PlcAddressMapper.TryGetInputSlot(loaderX, loaderY, out _, out _, out int nanoSlotNo))
            {
                await _repository.MarkNanoDoneAsync(CurrentTrayRunId, nanoSlotNo);
            }

            // 건조 시작 / 완료
            if (IsRisingEdge(_previousData?.L1_DryStartUpper, current.L1_DryStartUpper))
            {
                await _repository.MarkDryStartedAsync(CurrentTrayRunId);
            }

            if (IsRisingEdge(_previousData?.L2_DryStartLower, current.L2_DryStartLower))
            {
                await _repository.MarkDryStartedAsync(CurrentTrayRunId);
            }

            if (IsRisingEdge(_previousData?.L3_DryEndUpper, current.L3_DryEndUpper))
            {
                await _repository.MarkDryCompletedAsync(CurrentTrayRunId);
            }

            if (IsRisingEdge(_previousData?.L4_DryEndLower, current.L4_DryEndLower))
            {
                await _repository.MarkDryCompletedAsync(CurrentTrayRunId);
            }

            // 적층 source pick
            // D65 = Transfer X(열)
            // D66 = Unloader X(행)
            short transferX = current.PositionIndex[5]; // D65
            short unloaderX = current.PositionIndex[6]; // D66

            // 현재 그룹 번호 계산
            int currentGroupNo = PlcAddressMapper.GetCurrentGroupNo(
                current.D26_StackOutCount,
                current.D20_StackInputCount);

            // M906 상승엣지 시 1개 pick 완료로 기록
            if (IsRisingEdge(_previousData?.M906_StackInputDone, current.M906_StackInputDone) &&
                current.D20_StackInputCount >= 1 &&
                current.D20_StackInputCount <= 5 &&
                PlcAddressMapper.TryGetStackPickSlot(transferX, unloaderX, out _, out _, out int pickedSlotNo))
            {
                await _repository.RegisterStackPickAsync(
                    CurrentTrayRunId,
                    currentGroupNo,
                    pickedSlotNo,
                    current.D20_StackInputCount);
            }

            // M991 상승엣지 -> Dotting 로그
            if (IsRisingEdge(_previousData?.M991_DotDone, current.M991_DotDone))
            {
                await _repository.InsertDottingEventAsync(CurrentTrayRunId, currentGroupNo, current.D22_DottingCount);
            }

            // M922 상승/하강 -> UV 시작/종료 로그
            if (IsRisingEdge(_previousData?.M922_UVRun, current.M922_UVRun))
            {
                await _repository.InsertUvStartEventAsync(CurrentTrayRunId, currentGroupNo);
            }

            if (IsFallingEdge(_previousData?.M922_UVRun, current.M922_UVRun))
            {
                await _repository.InsertUvFinishEventAsync(CurrentTrayRunId, currentGroupNo);
            }

            // M937 상승엣지 -> 적층완료보드 안착 완료
            if (IsRisingEdge(_previousData?.M937_StackOutDone, current.M937_StackOutDone))
            {
                int completedGroupNo = current.D26_StackOutCount;
                if (completedGroupNo < 1)
                {
                    completedGroupNo = currentGroupNo;
                }

                short boardX = current.PositionIndex[6]; // D66 = Unloader X 11~15
                short boardY = current.PositionIndex[7]; // D67 = Unloader Y 11~12

                int boardRowNo;
                int boardColNo;

                if (!PlcAddressMapper.TryGetStackBoardPosition(boardX, boardY, out boardRowNo, out boardColNo))
                {
                    PlcAddressMapper.GetDefaultStackBoardPosition(completedGroupNo, out boardRowNo, out boardColNo);
                }

                await _repository.MarkStackGroupLaminatedAsync(
                    CurrentTrayRunId,
                    completedGroupNo,
                    boardRowNo,
                    boardColNo);

                bool trayCompleted = await _repository.CompleteTrayIfFinishedAsync(CurrentTrayRunId);
                if (trayCompleted)
                {
                    CurrentTrayRunId = await _repository.GetOrCreateCurrentTrayRunAsync();
                }
            }

            // 알람
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
                    CurrentTrayRunId = await _repository.GetOrCreateCurrentTrayRunAsync();
                }

                var trayInfo = await _repository.GetTrayRunInfoAsync(CurrentTrayRunId);

                var inputSlots = await _repository.GetInputSlotsAsync(CurrentTrayRunId);
                var boardCells = await _repository.GetStackBoardCellsAsync(CurrentTrayRunId);

                CurrentSnapshot = new MesSnapshot
                {
                    Dashboard = await _repository.GetDashboardSummaryAsync(),
                    History = await _repository.GetHistorySummaryAsync(),
                    InputSlots = inputSlots.Count > 0 ? inputSlots : MesUiFactory.CreateDefaultInputSlots(),
                    StackBoardCells = boardCells.Count > 0 ? boardCells : MesUiFactory.CreateDefaultStackBoardCells(),
                    Reports = await _repository.GetRecentReportsAsync(20),
                    CurrentTrayRunNo = string.IsNullOrWhiteSpace(trayInfo.TrayRunNo) ? "대기 중" : trayInfo.TrayRunNo,
                    CurrentTrayTypeText = string.IsNullOrWhiteSpace(trayInfo.TrayType)
                        ? ""
                        : PlcAddressMapper.GetTrayTypeText(trayInfo.TrayType)
                };
            }
            catch
            {
                CurrentSnapshot = new MesSnapshot
                {
                    Dashboard = new DashboardSummary(),
                    History = new HistorySummary(),
                    InputSlots = MesUiFactory.CreateDefaultInputSlots(),
                    StackBoardCells = MesUiFactory.CreateDefaultStackBoardCells(),
                    Reports = new(),
                    CurrentTrayRunNo = "대기 중",
                    CurrentTrayTypeText = ""
                };
            }

            SnapshotUpdated?.Invoke(this, EventArgs.Empty);
        }


        private async Task SafeAddRuntimeErrorReportAsync(string message)
        {
            try
            {
                await _repository.InsertEventAsync(
                    CurrentTrayRunId > 0 ? CurrentTrayRunId : null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "RUNTIME_ERROR",
                    null,
                    null,
                    message);

                await RefreshSnapshotAsync();
            }
            catch
            {
                // 여기서 또 예외가 나더라도 타이머를 멈추지 않음
            }
        }


        private static bool IsRisingEdge(bool? previous, bool current)
        {
            return previous.HasValue && previous.Value == false && current;
        }

        private static bool IsFallingEdge(bool? previous, bool current)
        {
            return previous.HasValue && previous.Value && !current;
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