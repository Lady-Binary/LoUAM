﻿using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using AssetStudio;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace LoUAM
{
    public partial class MapGenerator : Window
    {

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static string GameDirectory = "";
        public static bool MapGenerated = false;
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
            backgroundWorker.DoWork += new DoWorkEventHandler(backgroundWorker_DoWork);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);
            backgroundWorker.WorkerReportsProgress = true;
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            int percentComplete = 0;
            BackgroundWorker worker = sender as BackgroundWorker;

            string AssetFile = GameDirectory + "/Legends of Aria_Data/resources.assets";
            assetsManager.LoadFiles(AssetFile);
            
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                int count = 1;
                int total_assets = assetsFile.Objects.Count;
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
                    if ((int)Math.Round((double)(100 * count) / total_assets) > percentComplete)
                    {
                        percentComplete = (int)Math.Round((double)(100 * count) / total_assets);
                    }
                    count++;
                }
            }
            assetsManager.Clear();
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 100;
            MapGenerated = true;
            SaveSettings();
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GameDirctoryTextBox.Text = GameDirectory;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!MapGenerated)
            {
                if((MessageBoxEx.Show("You need to generate a map before you can use LoUAM\n\rDo you want to quit LoUAM?", "Quit LOUAM", MessageBoxButton.YesNo) == MessageBoxResult.Yes))
                {
                    Application.Current.Shutdown();
                }
                else
                {
                    e.Cancel = true;
                }
            }
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
                Debug.WriteLine(DefaultDirectory);
                if (Directory.Exists(DefaultDirectory))
                {
                    GameDirectoryDefault = DefaultDirectory;
                }
            }

            GameDirectory = (string)LoUAMKey.GetValue("GameDirectory", GameDirectoryDefault);
            MapGenerated = bool.TryParse(LoUAMKey.GetValue("MapGenerated", true).ToString(), out bool mgb) ? mgb : false;
        }

        public static void SaveSettings()
        {
            RegistryKey SoftwareKey = Registry.CurrentUser.OpenSubKey("Software", true);

            RegistryKey LoUAMKey = SoftwareKey.OpenSubKey("LoUAM", true);
            if (LoUAMKey == null)
            {
                LoUAMKey = SoftwareKey.CreateSubKey("LoUAM", true);
            }

            LoUAMKey.SetValue("GameDirectory", GameDirectory);
            LoUAMKey.SetValue("MapGenerated", MapGenerated);
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
                    GameDirctoryTextBox.Text = Folder;
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
                    ProgressBar.Visibility = Visibility.Visible;
                    ProgressBar.IsIndeterminate = true;
                    backgroundWorker.RunWorkerAsync();
                }
            }
            else
            {
                MessageBoxEx.Show("The selected folder is not a Legends of Aria game folder. \n\r Please select the folder that contains the 'Legends of Aria.exe' file");
            }
        }


        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
