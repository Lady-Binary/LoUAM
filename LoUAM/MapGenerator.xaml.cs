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

        public MapGenerator(string region, int TotalTiles)
        {
            this.Region = region;
            this.TotalTiles = TotalTiles;
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
            Directory.CreateDirectory(mapDirectory);

            ClientCommand Command = new ClientCommand((LoU.CommandType)Enum.Parse(typeof(LoU.CommandType), "ExportMap"));
            Command.CommandParams.Add("0",new ClientCommand.CommandParamStruct() { CommandParamType = ClientCommand.CommandParamTypeEnum.String, String = mapDirectory });
            MainWindow.ExecuteCommandAsync(Command);

            Stopwatch timeout = new Stopwatch();
            timeout.Start();
            while (GeneratedTiles < TotalTiles &&
                timeout.ElapsedMilliseconds < 120000)
            {
                GeneratedTiles = (Directory.GetFiles(mapDirectory, "*.json").Length + Directory.GetFiles(mapDirectory, "*.jpg").Length) / 2;
                UpdateProgress(0, GeneratedTiles, TotalTiles, "tiles");

                Thread.Sleep(100);
            }
            if (timeout.ElapsedMilliseconds >= 120000)
            {
                Debug.WriteLine("Timed out!");
                return;
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            GeneratedTiles = (Directory.GetFiles(mapDirectory, "*.json").Length + Directory.GetFiles(mapDirectory, "*.jpg").Length) / 2;
            if (GeneratedTiles < TotalTiles)
            {
                MessageBoxExShow(this, "Export completed, but map could be incomplete. This window will now close and the map will be loaded: it might take few minutes, depending on your computer.", "Map export incomplete");
            }
            else
            {
                MessageBoxExShow(this, "Export completed. This window will now close and the map will be loaded: it might take few minutes, depending on your computer.", "Map export completed");
            }
            Close();
        }

        private delegate void MessageBoxExShowDelegate(Window owner, string text, string caption);
        private void MessageBoxExShow(Window owner, string text, string caption)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new MessageBoxExShowDelegate(MessageBoxExShow), new object[] { owner, text, caption });
                return;
            }
            MessageBoxEx.Show(owner, text, caption);
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
            AssetsProgressTextBlock.Text = $"{value} out of {maximum} {item} processed.";
        }
    }
}
