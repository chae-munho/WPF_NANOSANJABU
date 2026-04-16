using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace NanoSanjabu.Views
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

            const int rows = 5;
            const int cols = 10;

            var items = new List<TrayItem>(rows * cols);

           
            for (int r = 0; r < rows; r++)         
            {
                for (int c = 0; c < cols; c++)     
                {
                    int trayNumber = (c * rows) + (rows - r); 

                    Brush brush;
                    string status;

                    if (trayNumber <= 3) { brush = (Brush)FindResource("StatusRun"); status = "STATUS: RUN"; }
                    else if (trayNumber <= 6) { brush = (Brush)FindResource("StatusComplete"); status = "STATUS: COMPLETE"; }
                    else { brush = (Brush)FindResource("StatusStop"); status = "STATUS: STOP"; }

                    items.Add(new TrayItem
                    {
                        TrayText = $"Lot {trayNumber}",
                        StatusText = status,
                        TimeText = "TIME: 0m",
                        StatusBrush = brush
                    });
                }
            }

            TrayItems.ItemsSource = items;
        }
    }
}
