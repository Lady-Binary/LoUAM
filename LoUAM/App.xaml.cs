using System.Windows;

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                await MainConsole.Start(e.Args);
                Shutdown();
            }
            else
            {
                base.OnStartup(e);
            }
        }
    }
}
