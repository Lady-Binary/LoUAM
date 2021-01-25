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
        private string mapDirectory;
        private BackgroundWorker backgroundWorker;

        private int GeneratedTransforms = 0;
        private int TotalTransforms = 0;

        private int GeneratedTextures = 0;
        private int TotalTextures = 0;

        public MapGenerator(int TotalTransforms, int TotalTextures)
        {
            this.TotalTransforms = TotalTransforms;
            this.TotalTextures = TotalTextures;
            mapDirectory = Path.GetFullPath(".\\MapData");
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
            while (GeneratedTransforms < TotalTransforms &&
                timeout.ElapsedMilliseconds < 120000)
            {
                GeneratedTransforms = Directory.GetFiles("./MapData/", "*.json").Length;
                UpdateProgress(0, GeneratedTransforms, TotalTransforms, "transforms");

                Thread.Sleep(100);
            }
            if (timeout.ElapsedMilliseconds >= 120000)
            {
                Debug.WriteLine("Timed out!");
                return;
            }
            timeout.Reset();
            while ((GeneratedTransforms < TotalTransforms || GeneratedTextures < TotalTextures) &&
                timeout.ElapsedMilliseconds < 120000)
            {
                GeneratedTextures = Directory.GetFiles("./MapData/", "*.jpg").Length;
                UpdateProgress(0, GeneratedTextures, TotalTextures, "textures");

                Thread.Sleep(100);
            }
            timeout.Stop();
            if (timeout.ElapsedMilliseconds >= 120000)
            {
                Debug.WriteLine("Timed out!");
                return;
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            GeneratedTransforms = Directory.GetFiles("./MapData/", "*.json").Length;
            GeneratedTextures = Directory.GetFiles("./MapData/", "*.jpg").Length;
            if (GeneratedTransforms < TotalTransforms || GeneratedTextures < TotalTextures)
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
