using System.Windows.Media;

namespace NanoSanjabu.Models
{
    public class StackBoardCellState
    {
        public int GroupNo { get; set; }

        public int RowNo { get; set; }
        public int ColNo { get; set; }

        public string TrayType { get; set; } = "";
        public string StatusCode { get; set; } = StackBoardStatus.Waiting;

        // UI 표시용
        public string LotText { get; set; } = "";
        public string MemberText { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string TimeText { get; set; } = "";

        public Brush StatusBrush { get; set; } = Brushes.Gray;
    }
}