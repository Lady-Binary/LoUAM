using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using AssetStudio;
using System.ComponentModel;
using System.Diagnostics;

namespace LoUAM
{
    public partial class MapGenerator : Window
    {
        public static string GameDirectory = "";
        private AssetsManager assetsManager;

        public static bool TrackPlayer = true;

        public static List<Marker> Places = new List<Marker>();
        private BackgroundWorker backgroundWorker;

        public MapGenerator()
        {
            assetsManager = new AssetsManager();
            backgroundWorker = new BackgroundWorker();
            InitializeComponent();
            InitializeBackgroundWorker();
            LoadSettings();
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

            string AssetFile = GameDirectory + "/Legends of Aria_Data/resources.assets";
            assetsManager.LoadFiles(AssetFile);

            int count = 0;
            int total_assets = 0;
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                total_assets += assetsFile.Objects.Count;
            }
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    if (asset.type == ClassIDType.Texture2D)
                    {
                        Texture2D TextureAsset = (Texture2D)asset;
                        if (TextureAsset.m_Name.StartsWith("Grid_"))
                        {
                            Exporter.ExportTexture2D(TextureAsset);
                        }
                    }
                    else if (asset.type == ClassIDType.GameObject)
                    {
                        GameObject gameObject = asset as GameObject;
                        if (gameObject.m_Name.StartsWith("Grid_"))
                        {
                            Exporter.ExportGameObject(gameObject.m_Name.Replace("Minimap",""),gameObject);
                        }
                    }
                    count++;
                    UpdateProgress(1, count, total_assets);
                }
            }
            assetsManager.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
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
            foreach(string DefaultDirectory in DefaultDirectories)
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

    }
}
