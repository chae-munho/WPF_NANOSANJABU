using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NanoSanjabu.Views;

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

         
            NavigateToDashboard();
        }

        private void AnyWhereDrag(object sender, MouseButtonEventArgs e)
        {
          
            if (e.OriginalSource is DependencyObject d)
            {
                while (d != null)
                {
                    if (d is ButtonBase || d is TextBoxBase || d is ScrollBar)
                        return;

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

        //  네비 버튼 클릭 
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

        //  Active 스타일 전환 
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
