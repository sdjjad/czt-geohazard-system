using System.Windows;
using System.Windows.Media;

namespace cztApp1
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 强制启用硬件（GPU）渲染
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            base.OnStartup(e);
            new SplashWindow().Show();
        }
    }
}
