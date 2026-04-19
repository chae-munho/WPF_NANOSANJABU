using NanoSanjabu.Services;
using System.Linq;
using System.Windows.Controls;

namespace NanoSanjabu.Views
{
    public partial class InputPartPage : Page
    {
        public InputPartPage()
        {
            InitializeComponent();

            TrayItems.ItemsSource = MesUiFactory.CreateDefaultInputSlots();
            TxtTotalCount.Text = "50";
            TxtWaitingCount.Text = "50";
            TxtRunningCount.Text = "0";
            TxtCompleteCount.Text = "0";
            TxtTrayTitle.Text = "대기 중";

            Loaded += InputPartPage_Loaded;
            Unloaded += InputPartPage_Unloaded;
        }

        private void InputPartPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AppServices.MesRuntimeService.SnapshotUpdated += MesRuntimeService_SnapshotUpdated;
            Render();
        }

        private void InputPartPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AppServices.MesRuntimeService.SnapshotUpdated -= MesRuntimeService_SnapshotUpdated;
        }

        private void MesRuntimeService_SnapshotUpdated(object? sender, System.EventArgs e)
        {
            Dispatcher.Invoke(Render);
        }

        private void Render()
        {
            var snapshot = AppServices.MesRuntimeService.CurrentSnapshot;
            var items = snapshot.InputSlots.OrderBy(x => x.ColNo).ThenBy(x => x.RowNo).ToList();

            TrayItems.ItemsSource = items;
            TxtTrayTitle.Text = string.IsNullOrWhiteSpace(snapshot.CurrentTrayLotNo) ? "대기 중" : $"{snapshot.CurrentTrayLotNo} - 작업 중";

            TxtTotalCount.Text = items.Count.ToString();
            TxtWaitingCount.Text = items.Count(x => x.StatusText.Contains("WAIT")).ToString();
            TxtRunningCount.Text = items.Count(x => x.StatusText.Contains("RUN") || x.StatusText.Contains("LOAD")).ToString();
            TxtCompleteCount.Text = items.Count(x => x.StatusText.Contains("COMPLETE")).ToString();
        }
    }
}