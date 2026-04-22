using System.Collections.Generic;

namespace NanoSanjabu.Models
{
    public class MesSnapshot
    {
        public DashboardSummary Dashboard { get; set; } = new();
        public HistorySummary History { get; set; } = new();

        // 현재 화면에 표시할 source tray 50슬롯
        public List<InputSlotState> InputSlots { get; set; } = new();

        // 적층완료보드 2행 5열
        public List<StackBoardCellState> StackBoardCells { get; set; } = new();

        public List<ReportItem> Reports { get; set; } = new();

        public string CurrentTrayRunNo { get; set; } = "대기 중";
        public string CurrentTrayTypeText { get; set; } = "";
    }
}