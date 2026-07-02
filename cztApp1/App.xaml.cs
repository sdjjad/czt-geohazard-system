using System.Windows;

namespace cztApp1
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new SplashWindow().Show();
        }
    }
}
