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

            // 화면 표시 순서:
            // 상단 6~10, 하단 1~5
            var items = snapshot.StackGroups
                .OrderBy(x => x.GroupNo <= 5 ? x.GroupNo + 5 : x.GroupNo - 5)
                .ToList();

            StackGroupItemsControl.ItemsSource = items;

            TxtTotalGroupCount.Text = snapshot.StackGroups.Count.ToString();
            TxtRunningGroupCount.Text = snapshot.StackGroups.Count(x => x.ModeText == "RUN").ToString();
            TxtCompletedGroupCount.Text = snapshot.StackGroups.Count(x => x.ModeText == "COMPLETE").ToString();
        }
    }
}