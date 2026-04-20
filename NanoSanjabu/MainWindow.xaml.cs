using NanoSanjabu.Services;
using NanoSanjabu.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NanoSanjabu
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            NavigateToDashboard();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                bool connected = await AppServices.DatabaseService.TestConnectionAsync();
                int result = await AppServices.DatabaseService.ExecuteScalarTestAsync();

                MessageBox.Show(
                    $"로컬 DB 연결 성공\nConnected: {connected}\nSELECT 1 결과: {result}",
                    "DB 테스트",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"DB 연결 실패\n\n{ex.Message}",
                    "DB 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            try
            {
                await AppServices.MesRuntimeService.InitializeSnapshotAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"초기 데이터 로딩 실패\n\n{ex.Message}",
                    "초기화 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            try
            {
                var plcService = new PlcService();
                AppServices.AttachPlcService(plcService);
                await AppServices.MesRuntimeService.StartPlcAsync();

                MessageBox.Show(
                    "PLC 연결 성공",
                    "PLC 상태",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"PLC 미연결 상태로 실행합니다.\n\n{ex.Message}",
                    "PLC 상태",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject d)
            {
                while (d != null)
                {
                    if (d is ButtonBase || d is TextBoxBase)
                    {
                        return;
                    }

                    d = VisualTreeHelper.GetParent(d);
                }
            }

            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, new RoutedEventArgs());
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeIcon.Text = "\uE922";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeIcon.Text = "\uE923";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            NavigateToDashboard();
        }

        private void BtnInputPart_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnNavInputPart);
            MainFrame.Navigate(new InputPartPage());
        }

        private void BtnLaminated_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnNavLaminated);
            MainFrame.Navigate(new LaminatedSectionPage());
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnNavHistory);
            MainFrame.Navigate(new HistoryPage());
        }

        private void NavigateToDashboard()
        {
            SetActiveNav(BtnNavDashboard);
            MainFrame.Navigate(new DashboardPage());
        }

        private void SetActiveNav(Button active)
        {
            BtnNavDashboard.Style = (Style)FindResource("NavItem");
            BtnNavInputPart.Style = (Style)FindResource("NavItem");
            BtnNavLaminated.Style = (Style)FindResource("NavItem");
            BtnNavHistory.Style = (Style)FindResource("NavItem");

            active.Style = (Style)FindResource("NavItemActive");
        }
    }
}