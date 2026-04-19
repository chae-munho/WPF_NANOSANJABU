using NanoSanjabu.Services;
using System.Windows;

namespace NanoSanjabu
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            await AppServices.InitializeAsync();

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppServices.MesRuntimeService?.Dispose();
            base.OnExit(e);
        }
    }
}