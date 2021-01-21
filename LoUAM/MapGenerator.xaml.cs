using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;
using LoU;
using System.Threading;

namespace LoUAM
{
    public partial class MapGenerator : Window
    {
        public static string GameDirectory = "";
        private string mapDirectory;
        private BackgroundWorker backgroundWorker;

        public MapGenerator()
        {
            mapDirectory = Path.GetFullPath(".\\MapData");
            backgroundWorker = new BackgroundWorker();
            InitializeComponent();
            this.DataContext = Application.Current.MainWindow;
            InitializeBackgroundWorker();
            LoadSettings();
            SaveSettings();
        }

        private void InitializeBackgroundWorker()
        {
            backgroundWorker.DoWork += new DoWorkEventHandler(BackgroundWorker_DoWork);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorker_RunWorkerCompleted);
            backgroundWorker.WorkerReportsProgress = true;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            Directory.CreateDirectory(mapDirectory);

            ClientCommand Command = new ClientCommand((LoU.CommandType)Enum.Parse(typeof(LoU.CommandType), "ExportMap"));
            Command.CommandParams.Add("0",new ClientCommand.CommandParamStruct() { CommandParamType = ClientCommand.CommandParamTypeEnum.String, String = mapDirectory });

            int ClientCommandId = 0;
            Queue<ClientCommand> ClientCommandsQueue;
            ClientCommand[] ClientCommandsArray;
            MainWindow.ClientCommandsMemoryMap.ReadMemoryMap(out ClientCommandId, out ClientCommandsArray);
            if (ClientCommandsArray == null)
            {
                ClientCommandsQueue = new Queue<ClientCommand>();
            }
            else
            {
                ClientCommandsQueue = new Queue<ClientCommand>(ClientCommandsArray);
            }

            if (ClientCommandsQueue.Count > 100)
            {
                throw new Exception("Too many commands in the queue. Cannot continue.");
            }

            ClientCommandsQueue.Enqueue(Command);
            int AssignedClientCommandId = ClientCommandId + ClientCommandsQueue.Count;
            MainWindow.ClientCommandsMemoryMap.WriteMemoryMap(ClientCommandId, ClientCommandsQueue.ToArray());
            Debug.WriteLine("Command inserted, assigned CommandId=" + AssignedClientCommandId.ToString());

            Stopwatch timeout = new Stopwatch();
            timeout.Start();
            while (ClientCommandId < AssignedClientCommandId && timeout.ElapsedMilliseconds < 30000)
            {
                Debug.WriteLine("Waiting for command to be executed, Current CommandId=" + ClientCommandId.ToString() + ", Assigned CommandId=" + AssignedClientCommandId.ToString());
                Thread.Sleep(50);
                MainWindow.ClientCommandsMemoryMap.ReadMemoryMap(out ClientCommandId, out ClientCommandsArray);
            }
            timeout.Stop();
            if (timeout.ElapsedMilliseconds >= 3000)
            {
                Debug.WriteLine("Timed out!");
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBoxEx.Show(this, "Export completed. You can now close this window.", "Assets export completed");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GameDirectoryTextBox.Text = GameDirectory;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
        }

        public static void LoadSettings()
        {
            string[] DefaultDirectories = {
                "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Legends of Aria",
                "C:\\Program Files\\Legends of Aria Launcher"
            };

            RegistryKey SoftwareKey = Registry.CurrentUser.OpenSubKey("Software", true);

            RegistryKey LoUAMKey = SoftwareKey.OpenSubKey("LoUAM", true);
            if (LoUAMKey == null)
            {
                LoUAMKey = SoftwareKey.CreateSubKey("LoUAM", true);
            }

            string GameDirectoryDefault = "";
            foreach (string DefaultDirectory in DefaultDirectories)
            {
                if (Directory.Exists(DefaultDirectory))
                {
                    GameDirectoryDefault = DefaultDirectory;
                }
            }

            GameDirectory = (string)LoUAMKey.GetValue("GameDirectory", GameDirectoryDefault);
        }

        public static void SaveSettings()
        {
            RegistryKey SoftwareKey = Registry.CurrentUser.OpenSubKey("Software", true);

            RegistryKey LoUAMKey = SoftwareKey.OpenSubKey("LoUAM", true);
            if (LoUAMKey == null)
            {
                LoUAMKey = SoftwareKey.CreateSubKey("LoUAM", true);
            }

            RegistryKey LoUKey = SoftwareKey.OpenSubKey("LoU", true);
            if (LoUKey == null)
            {
                LoUKey = SoftwareKey.CreateSubKey("LoU", true);
            }

            ((App)Application.Current).GameDirectory = GameDirectory;
            LoUKey.SetValue("GameDirectory", GameDirectory);
            LoUAMKey.SetValue("GameDirectory", GameDirectory);
            LoUAMKey.SetValue("WorkingDirectory", Directory.GetCurrentDirectory());
        }

        private void GameDirectoryBrowse_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Please select a folder.";
            dialog.UseDescriptionForTitle = true;
            if ((bool)dialog.ShowDialog(this))
            {
                string Folder = dialog.SelectedPath;
                if (!File.Exists(Folder + "\\Legends of Aria.exe"))
                {
                    MessageBoxEx.Show("The selected folder is not a Legends of Aria game folder. \n\r Please select the folder that contains the 'Legends of Aria.exe' file");
                }
                else
                {
                    GameDirectoryTextBox.Text = Folder;
                    GameDirectory = Folder;
                    SaveSettings();
                }
            }
        }

        private void GenerateMap_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(GameDirectory + "\\Legends of Aria.exe"))
            {
                if (backgroundWorker.IsBusy != true)
                {
                    BrowseButton.IsEnabled = false;
                    GenerateMapButton.IsEnabled = false;
                    ConnectToClientButton.IsEnabled = false;
                    AssetsProgressBar.Visibility = Visibility.Visible;
                    AssetsProgressBar.IsIndeterminate = true;
                    backgroundWorker.RunWorkerAsync();
                }
            }
            else
            {
                MessageBoxEx.Show("The selected folder is not a Legends of Aria game folder. \n\r Please select the folder that contains the 'Legends of Aria.exe' file");
            }
        }
        

        private delegate void UpdateProgressDelegate(double Minimum, double Value, double Maximum);
        private void UpdateProgress(double Minimum, double Value, double Maximum)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new UpdateProgressDelegate(UpdateProgress), Minimum, Value, Maximum);
                return;
            }
            AssetsProgressBar.IsIndeterminate = false;
            AssetsProgressBar.Minimum = Minimum;
            AssetsProgressBar.Value = Value;
            AssetsProgressBar.Maximum = Maximum;

            AssetsProgressLabel.Content = $"{Value} out of {Maximum} assets processed.";
        }

        private void ConnectToClient_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Owner;
            mainWindow.DoConnectToLoAClientCommand();
        }
    }
}
