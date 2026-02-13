using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NanoSanjabu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
        new MouseButtonEventHandler(AnyWhereDrag),
        true);
        }
        private void AnyWhereDrag(object sender, MouseButtonEventArgs e)
        {
            // 버튼 같은 곳 눌렀을 때 창이 끌려가면 불편하니까,
            // 버튼 위 클릭은 제외 (원하면 제거 가능)
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


        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeIcon.Text = "\uE922"; // Maximize 아이콘
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeIcon.Text = "\uE923"; // Restore 아이콘
            }

        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 더블클릭: 최대화/복원 토글
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, new RoutedEventArgs());
                return;
            }

            // 드래그로 창 이동
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }

        }
    }
}