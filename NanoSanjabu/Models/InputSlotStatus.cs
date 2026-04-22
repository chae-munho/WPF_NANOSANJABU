using System.Windows.Media;

namespace NanoSanjabu.Models
{
    public class InputSlotState
    {
        public int SlotNo { get; set; }
        public int RowNo { get; set; }
        public int ColNo { get; set; }

        public string TrayType { get; set; } = "";
        public string StatusCode { get; set; } = InputSlotStatus.Waiting;

        // UI 표시용
        public string LotText { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string TimeText { get; set; } = "";
        public string CompletedAtText { get; set; } = "";

        public Brush StatusBrush { get; set; } = Brushes.Gray;
    }
}