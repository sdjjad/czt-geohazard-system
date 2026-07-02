using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace cztApp1
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (FindResource("DotAnim") is Storyboard sb)
                    sb.Begin();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    new MainWindow { WindowState = WindowState.Maximized }.Show();
                    Close();
                };
                timer.Start();
            };
        }
    }
}
