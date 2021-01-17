using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace LoUAM
{
    public partial class App : Application
    {

        public string GameDirectory = "";

        void App_Startup(object sender, StartupEventArgs e)
        {
            MapGenerator.LoadSettings();
            GameDirectory = MapGenerator.GameDirectory;
            var domain = AppDomain.CurrentDomain;
            domain.AssemblyResolve += LoadAssembly;

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private Assembly LoadAssembly(object sender, ResolveEventArgs args)
        {
            Assembly result = null;
            if (args != null && !string.IsNullOrEmpty(args.Name))
            {
                FileInfo info = new FileInfo(Assembly.GetExecutingAssembly().Location);
                string  DLLDirectory = GameDirectory + @"\Legends of Aria_Data\Managed";
                string  assemblyName = args.Name.Split(new string[] { "," }, StringSplitOptions.None)[0];
                string  assemblyExtension = "dll";
                string  assemblyPath = Path.Combine(DLLDirectory, string.Format("{0}.{1}", assemblyName, assemblyExtension));

                if (File.Exists(assemblyPath))
                {
                    result = Assembly.LoadFrom(assemblyPath);
                }
                else
                {
                    return args.RequestingAssembly;
                }
            }

            return result;
        }

    }
}
