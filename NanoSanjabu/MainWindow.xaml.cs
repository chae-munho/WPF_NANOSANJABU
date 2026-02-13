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

            AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(AnyWhereDrag),
                true);

            // ✅ 시작은 대시보드
            NavigateToDashboard();
        }

        private void AnyWhereDrag(object sender, MouseButtonEventArgs e)
        {
            // 버튼/텍스트박스/스크롤바 위 클릭은 드래그 제외
            if (e.OriginalSource is DependencyObject d)
            {
                while (d != null)
                {
                    if (d is ButtonBase || d is TextBoxBase || d is ScrollBar)
                        return;

                    d = VisualTreeHelper.GetParent(d);
                }
            }

            // 더블클릭: 최대화/복원
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, new RoutedEventArgs());
                return;
            }

            // 드래그 이동
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, new RoutedEventArgs());
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // ====== 창 제어 ======
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

        // ====== 네비 버튼 클릭 ======
        private void BtnDashboard_Click(object sender, RoutedEventArgs e) => NavigateToDashboard();

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

        // ====== Active 스타일 전환 ======
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
