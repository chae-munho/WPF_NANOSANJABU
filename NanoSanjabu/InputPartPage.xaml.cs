using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace NanoSanjabu
{
    public partial class InputPartPage : Page
    {
        public class TrayItem
        {
            public string TrayText { get; set; } = "";
            public string StatusText { get; set; } = "";
            public string TimeText { get; set; } = "";
            public Brush StatusBrush { get; set; } = Brushes.Gray;
        }

        public InputPartPage()
        {
            InitializeComponent();

            // 테스트 데이터 50개 생성 (5x10)
            var items = new List<TrayItem>();
            for (int i = 1; i <= 50; i++)
            {
                // 예시로 1~3은 RUN, 4~6은 COMPLETE, 나머지는 STOP
                Brush brush;
                string status;

                if (i <= 3) { brush = (Brush)FindResource("StatusRun"); status = "STATUS: RUN"; }
                else if (i <= 6) { brush = (Brush)FindResource("StatusComplete"); status = "STATUS: COMPLETE"; }
                else { brush = (Brush)FindResource("StatusStop"); status = "STATUS: STOP"; }

                items.Add(new TrayItem
                {
                    TrayText = $"Tray {i}",
                    StatusText = status,
                    TimeText = "TIME: 0m",
                    StatusBrush = brush
                });
            }

            TrayItems.ItemsSource = items;
        }
    }
}
