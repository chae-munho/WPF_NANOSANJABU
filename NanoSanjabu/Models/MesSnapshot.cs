using System.Collections.Generic;

namespace NanoSanjabu.Models
{
    public class MesSnapshot
    {
        public DashboardSummary Dashboard { get; set; } = new();
        public HistorySummary History { get; set; } = new();
        public List<InputSlotState> InputSlots { get; set; } = new();
        public List<StackGroupState> StackGroups { get; set; } = new();
        public List<ReportItem> Reports { get; set; } = new();
        public string CurrentTrayLotNo { get; set; } = "대기 중";
    }
}