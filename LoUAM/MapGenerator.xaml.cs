using System;
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
        private string mapDirectory;
        private BackgroundWorker backgroundWorker;

        private string Region = "";

        private int GeneratedTiles = 0;
        private int TotalTiles = 0;

        public MapGenerator(string region)
        {
            this.Region = region;
            mapDirectory = Path.GetFullPath($"{Map.MAP_DATA_FOLDER}/{region}");
            backgroundWorker = new BackgroundWorker();
            InitializeComponent();
            InitializeBackgroundWorker();
            StartBackgroundWorker();
        }

        private void InitializeBackgroundWorker()
        {
            backgroundWorker.DoWork += new DoWorkEventHandler(BackgroundWorker_DoWork);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorker_RunWorkerCompleted);
            backgroundWorker.WorkerReportsProgress = true;
        }

        private void StartBackgroundWorker()
        {
            AssetsProgressBar.Visibility = Visibility.Visible;
            AssetsProgressBar.IsIndeterminate = true;
            backgroundWorker.RunWorkerAsync();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            MainWindow.ExecuteCommand(new ClientCommand(CommandType.LoadMap, "region", Region));

            // Wait for scene / tiles to be loaded
            Stopwatch timeout = new Stopwatch();
            timeout.Start();
            TotalTiles = 0;
            while (TotalTiles == 0 && timeout.ElapsedMilliseconds < 30000)
            {
                MainWindow.RefreshClientStatus();
                lock (MainWindow.ClientStatusLock)
                {
                    TotalTiles = MainWindow.ClientStatus?.Miscellaneous.LOADEDMAPTILES ?? 0;
                }
                Thread.Sleep(1000);
            }
            timeout.Stop();

            Directory.CreateDirectory(mapDirectory);
            Timer timer = new Timer((state) =>
            {
                GeneratedTiles = (Directory.GetFiles(mapDirectory, "*.json").Length + Directory.GetFiles(mapDirectory, "*.jpg").Length) / 2;
                UpdateProgress(0, GeneratedTiles, TotalTiles, "tiles");
            }, null, 50, 50);

            MainWindow.ExecuteCommand(new ClientCommand(CommandType.ExportMap, "mapDirectory", mapDirectory), 120000);

            MainWindow.ExecuteCommand(new ClientCommand(CommandType.UnloadMap, "region", Region));
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            GeneratedTiles = (Directory.GetFiles(mapDirectory, "*.json").Length + Directory.GetFiles(mapDirectory, "*.jpg").Length) / 2;
            if (TotalTiles == 0 || GeneratedTiles < TotalTiles)
            {
                Close(false);
            }
            else
            {
                Close(true);
            }
        }

        private delegate void CloseDelegate(bool dialogResult);
        private void Close(bool dialogResult)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new CloseDelegate(Close), new object[] { dialogResult });
                return;
            }
            this.DialogResult = dialogResult;
            this.Close();
        }

        private delegate void UpdateProgressDelegate(double minimum, double value, double maximum, string item);
        private void UpdateProgress(double minimum, double value, double maximum, string item)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new UpdateProgressDelegate(UpdateProgress), minimum, value, maximum, item);
                return;
            }
            AssetsProgressBar.IsIndeterminate = false;
            AssetsProgressBar.Minimum = minimum;
            AssetsProgressBar.Value = value;
            AssetsProgressBar.Maximum = maximum;
            AssetsProgressTextBlock.Text = $"Region {Region}, {value} out of {maximum} {item} processed.";
        }
    }
}
