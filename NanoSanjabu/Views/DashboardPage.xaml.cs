using NanoSanjabu.Services;
using System.Windows.Controls;

namespace NanoSanjabu.Views
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();

            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
        }

        private void DashboardPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AppServices.MesRuntimeService.SnapshotUpdated += MesRuntimeService_SnapshotUpdated;
            Render();
        }

        private void DashboardPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
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

            TxtProductionCount.Text = snapshot.Dashboard.ProductionCount.ToString();
            TxtPassRate.Text = $"{snapshot.Dashboard.PassRate:0.#}%";
            TxtDefectRate.Text = $"{snapshot.Dashboard.DefectRate:0.#}%";
            TxtCompletedTrayCount.Text = snapshot.Dashboard.CompletedTrayCount.ToString();

            ReportItemsControl.ItemsSource = snapshot.Reports;
        }
    }
}