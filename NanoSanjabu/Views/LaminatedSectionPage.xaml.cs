using NanoSanjabu.Services;
using System.Linq;
using System.Windows.Controls;

namespace NanoSanjabu.Views
{
    public partial class LaminatedSectionPage : Page
    {
        public LaminatedSectionPage()
        {
            InitializeComponent();

            Loaded += LaminatedSectionPage_Loaded;
            Unloaded += LaminatedSectionPage_Unloaded;
        }

        private void LaminatedSectionPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AppServices.MesRuntimeService.SnapshotUpdated += MesRuntimeService_SnapshotUpdated;
            Render();
        }

        private void LaminatedSectionPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
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
            var items = snapshot.StackGroups.OrderBy(x => x.GroupNo).ToList();

            StackGroupItemsControl.ItemsSource = items;

            TxtTotalGroupCount.Text = items.Count.ToString();
            TxtRunningGroupCount.Text = items.Count(x => x.ModeText == "RUN").ToString();
            TxtCompletedGroupCount.Text = items.Count(x => x.ModeText == "COMPLETE").ToString();
        }
    }
}