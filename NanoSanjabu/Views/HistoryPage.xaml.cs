using NanoSanjabu.Services;
using System.Windows.Controls;

namespace NanoSanjabu.Views
{
    public partial class HistoryPage : Page
    {
        public HistoryPage()
        {
            InitializeComponent();

            Loaded += HistoryPage_Loaded;
            Unloaded += HistoryPage_Unloaded;
        }

        private void HistoryPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AppServices.MesRuntimeService.SnapshotUpdated += MesRuntimeService_SnapshotUpdated;
            Render();
        }

        private void HistoryPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
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

            TxtTotalLotCount.Text = snapshot.History.TotalLotCount.ToString();
            TxtTotalProducedUnit.Text = snapshot.History.TotalProducedUnit.ToString();
            TxtAvgProcessMinutes.Text = snapshot.History.AverageProcessMinutes.ToString();
            TxtReworkLotCount.Text = snapshot.History.ReworkLotCount.ToString();

            HistoryReportItemsControl.ItemsSource = snapshot.Reports;
        }
    }
}