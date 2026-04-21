using System.Windows.Media;

namespace NanoSanjabu.Models
{
    public class StackGroupState
    {
        public int GroupNo { get; set; }
        public int StartSlotNo { get; set; }
        public int EndSlotNo { get; set; }

        public int RowNo { get; set; }
        public int ColNo { get; set; }

        public string StatusCode { get; set; } = SlotStatus.Waiting;

        // 화면 위치 표시용
        public string LotText { get; set; } = "";

        // 완료 후 실제 멤버 목록 표시용
        public string RangeText { get; set; } = "";

        public string StatusText { get; set; } = "";
        public string ModeText { get; set; } = "";
        public string TimeText { get; set; } = "";

        public Brush StatusBrush { get; set; } = Brushes.Gray;
    }
}