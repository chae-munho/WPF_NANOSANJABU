using System.Windows.Media;

namespace NanoSanjabu.Models
{
    public class StackGroupState
    {
        public int GroupNo { get; set; }
        public int StartSlotNo { get; set; }
        public int EndSlotNo { get; set; }
        public string LotText { get; set; } = "";
        public string RangeText { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string ModeText { get; set; } = "";
        public string TimeText { get; set; } = "";
        public Brush StatusBrush { get; set; } = Brushes.Gray;
    }
}