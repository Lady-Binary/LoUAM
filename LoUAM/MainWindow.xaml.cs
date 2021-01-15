using LoU;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SharpMonoInjector;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows.Input;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AssetStudio;
using Microsoft.Win32;

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow TheMainWindow;

        private static Server theServer;
        internal static Server TheServer { get => theServer; set => theServer = value; }

        private static Client theClient;
        internal static Client TheClient { get => theClient; set => theClient = value; }

        public static int CurrentClientProcessId = -1;

        private static String ClientStatusMemoryMapMutexName;
        private static String ClientStatusMemoryMapName;
        private static Int32 ClientStatusMemoryMapSize;
        public static MemoryMap ClientStatusMemoryMap;

        private static String ClientCommandsMemoryMapMutexName;
        private static String ClientCommandsMemoryMapName;
        private static Int32 ClientCommandsMemoryMapSize;
        public static MemoryMap ClientCommandsMemoryMap;

        public static object ClientStatusLock = new object();
        public static ClientStatus ClientStatus;

        DispatcherTimer TimerRefreshCurrentPlayer;

        DispatcherTimer TimerUpdateServer;

        public MainWindow()
        {
            MainWindow.TheMainWindow = this;

            assetsManager = new AssetsManager();

            TimerRefreshCurrentPlayer = new DispatcherTimer();
            TimerRefreshCurrentPlayer.Tick += TimerRefreshCurrentPlayer_Tick;
            TimerRefreshCurrentPlayer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            TimerRefreshCurrentPlayer.Start();

            TimerUpdateServer = new DispatcherTimer();
            TimerUpdateServer.Tick += TimerUpdateServer_TickAsync;
            TimerUpdateServer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            TimerUpdateServer.Start();

            InitializeComponent();

            this.Title = "LoUAM - " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ControlPanel.LoadSettings();
            ControlPanel.SaveSettings();
            TrackPlayerMenu.IsChecked = ControlPanel.TrackPlayer;
            ControlPanel.LoadPlaces();
            MainMap.UpdateAllMarkersOfType(MarkerType.Place, ControlPanel.Places);
        }

        private void UpdateMainStatus(System.Windows.Media.Color color, string message)
        {
            if (MainStatusLabel.Content.ToString() != message)
            {
                MainStatusLabel.Foreground = new SolidColorBrush(color);
                MainStatusLabel.Content = message;
            }
        }

        private void UpdateLinkStatus(System.Windows.Media.Color color, string message)
        {
            if (LinkStatusLabel.Content.ToString() != message)
            {
                LinkStatusLabel.Foreground = new SolidColorBrush(color);
                LinkStatusLabel.Content = message;
            }
        }

        #region Timers
        //private static Random rnd = new Random();
        //private static Player MockPlayer = new Player(
        //        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        //        (ulong)rnd.Next(1, 1000000),
        //        $"LadyBinary",
        //        (ulong)rnd.Next(1, 1000),
        //        (ulong)rnd.Next(1, 1000),
        //        (ulong)rnd.Next(1, 1000));

        private Player GetCurrentPlayer()
        {
            //MockPlayer.LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            //return MockPlayer;

            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
                return null;

            Player currentPlayer = null;

            lock (ClientStatusLock)
            {
                ClientStatusMemoryMap.ReadMemoryMap<ClientStatus>(out ClientStatus);
                if (ClientStatus != null)
                {
                    if (new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - ClientStatus.TimeStamp <= 60000)
                    {
                        UpdateMainStatus(Colors.Green, $"Connected to Legends of Aria game client {MainWindow.CurrentClientProcessId.ToString()}.");

                        if (ClientStatus.CharacterInfo.CHARID != null)
                        {
                            currentPlayer = new Player(
                                ClientStatus.TimeStamp,
                                ClientStatus.CharacterInfo.CHARID ?? 0,
                                TheClient != null || TheServer != null ? ControlPanel.MyName : ClientStatus.CharacterInfo.CHARNAME,
                                ClientStatus.CharacterInfo.CHARPOSX ?? 0,
                                ClientStatus.CharacterInfo.CHARPOSY ?? 0,
                                ClientStatus.CharacterInfo.CHARPOSZ ?? 0);

                            Regex rx = new Regex(@"\[(.*?)\]");

                            // Try to extract the color
                            String CharColor = rx.Match(currentPlayer.DisplayName).Value;

                            // Clean up the name
                            String CharName = rx.Replace(currentPlayer.DisplayName, "");

                            currentPlayer.DisplayName = CharName;
                        }
                    }
                    else
                    {
                        UpdateMainStatus(Colors.Red, $"Client {MainWindow.CurrentClientProcessId.ToString()} not responding!");
                    }
                }
            }

            return currentPlayer;
        }

        public void RefreshCurrentPlayer()
        {
            //Debug.Print("RefreshCurrentPlayer");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Player currentPlayer = GetCurrentPlayer();

            if (currentPlayer != null)
            {
                Marker currentPlayerMarker = new Marker(
                    MarkerType.CurrentPlayer,
                    currentPlayer.ObjectId.ToString(),
                    MarkerIcon.none,
                    currentPlayer.DisplayName,
                    currentPlayer.X,
                    currentPlayer.Y,
                    currentPlayer.Z
                    );
                MainMap.UpdateAllMarkersOfType(MarkerType.CurrentPlayer, new[] { currentPlayerMarker });

                if (ControlPanel.TrackPlayer)
                {
                    MainMap.Center(currentPlayerMarker.X, currentPlayerMarker.Z);
                }
            }
            else
            {
                MainMap.RemoveAllMarkersOfType(MarkerType.CurrentPlayer);
            }

            stopwatch.Stop();
            //Console.WriteLine("RefreshClientStatus completed in {0} ms", stopwatch.ElapsedMilliseconds);
        }
        private void TimerRefreshCurrentPlayer_Tick(object sender, EventArgs e)
        {
            //Debug.Print("TimerRefreshCurrentPlayer_Tick()");

            //Debug.Print("this.TimerRefreshCurrentPlayer.Stop();");
            TimerRefreshCurrentPlayer.Stop();

            //Debug.Print("RefreshCurrentPlayer()");
            RefreshCurrentPlayer();

            //Debug.Print("TimerRefreshCurrentPlayer.Start();");
            TimerRefreshCurrentPlayer.Start();
        }

        public async Task UpdateServerAsync()
        {
            if (TheClient == null && TheServer == null)
            {
                UpdateLinkStatus(Colors.Black, "LoUAM Link not connected.");
                return;
            }

            Player currentPlayer = GetCurrentPlayer();
            if (currentPlayer != null && ControlPanel.MyName.Trim() != "" && ControlPanel.MyName.Trim() != "(your name)")
            {
                currentPlayer.DisplayName = ControlPanel.MyName;
            }

            if (TheServer != null)
            {
                UpdateLinkStatus(Colors.Blue, $"LoUAM Link Server listening on port ${ControlPanel.Port}...");
                lock (TheServer.PlayersLock)
                {
                    if (TheServer.Players != null && currentPlayer != null)
                    {
                        TheServer.Players[currentPlayer.ObjectId] = currentPlayer;
                        List<Marker> OtherMarkers = TheServer.Players.Values
                        .Where(player => player.ObjectId != currentPlayer.ObjectId)
                        .Select(player => new Marker(MarkerType.OtherPlayer, player.ObjectId.ToString(), MarkerIcon.none, player.DisplayName, player.X, player.Y, player.Z)).ToList();
                        MainMap.UpdateAllMarkersOfType(MarkerType.OtherPlayer, OtherMarkers);
                    }
                }
            }

            if (TheClient != null)
            {
                if (TheClient.ClientState != Client.ClientStateEnum.Connected)
                {
                    try
                    {
                        UpdateLinkStatus(Colors.Orange, "LoUAM Link connecting...");
                        await TheClient.ConnectAsync();
                        if (TheClient.ClientState != Client.ClientStateEnum.Connected)
                        {
                            UpdateLinkStatus(Colors.Red, "LoUAM Link disconnected, could not connect!");
                            TheClient.Disconnect();
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateLinkStatus(Colors.Red, $"LoUAM Link disconnected: {ex.Message}");
                        TheClient.Disconnect();
                        return;
                    }
                }
                UpdateLinkStatus(Colors.Green, $"LoUAM Link connected to {ControlPanel.Host} on port {ControlPanel.Port}");

                if (currentPlayer != null)
                {
                    try
                    {
                        TheClient.UpdatePlayer(currentPlayer);
                    }
                    catch (Exception ex)
                    {
                        UpdateLinkStatus(Colors.Red, $"LoUAM Link disconnected: {ex.Message}");
                        TheClient.Disconnect();
                        return;
                    }
                }

                try
                {
                    IEnumerable<Player> OtherPlayers = await TheClient.RetrievePlayers();
                    List<Marker> OtherMarkers = OtherPlayers
                        .Where(player => player.ObjectId != currentPlayer.ObjectId)
                        .Select(player => new Marker(MarkerType.OtherPlayer, player.ObjectId.ToString(), MarkerIcon.none, player.DisplayName, player.X, player.Y, player.Z)).ToList();
                    MainMap.UpdateAllMarkersOfType(MarkerType.OtherPlayer, OtherMarkers);
                }
                catch (Exception ex)
                {
                    UpdateLinkStatus(Colors.Red, $"LoUAM Link disconnected: {ex.Message}");
                    TheClient.Disconnect();
                    return;
                }
            }
        }
        private async void TimerUpdateServer_TickAsync(object sender, EventArgs e)
        {
            //Debug.Print("UpdateServer_Tick()");

            //Debug.Print("TimerUpdateServer.Stop();");
            TimerUpdateServer.Stop();

            //Debug.Print("UpdateServer()");
            await UpdateServerAsync();

            //Debug.Print("TimerUpdateServer.Start();");
            TimerUpdateServer.Start();
        }
        #endregion

        #region Injection
        private IntPtr GetMonoModule(int ProcessId)
        {
            UpdateMainStatus(Colors.Orange, "Getting Mono Module...");

            IntPtr MonoModule = new IntPtr();
            string Name;

            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id != ProcessId)
                    continue;

                const ProcessAccessRights flags = ProcessAccessRights.PROCESS_QUERY_INFORMATION | ProcessAccessRights.PROCESS_VM_READ;
                IntPtr handle;

                if ((handle = Native.OpenProcess(flags, false, p.Id)) != IntPtr.Zero)
                {
                    if (ProcessUtils.GetMonoModule(handle, out IntPtr mono))
                    {
                        MonoModule = mono;
                        Name = p.ProcessName;
                    }

                    Native.CloseHandle(handle);
                }
            }

            UpdateMainStatus(Colors.Orange, "Process refreshed");

            return MonoModule;
        }

        private static void Log(string s)
        {
            using (StreamWriter w = File.AppendText("c:\\log.txt"))
            {
                w.WriteLine(s);
            }
        }
        private void Inject(int ProcessId)
        {
            Log("1");            
            var MonoModule = GetMonoModule(ProcessId);

            String AssemblyPath = "LoU.dll";

            Log("2");
            IntPtr handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, ProcessId);

            if (handle == IntPtr.Zero)
            {
                Log("3");
                UpdateMainStatus(Colors.Red, "Failed to open process");
                return;
            }

            byte[] file;

            try
            {
                Log("4");
                file = File.ReadAllBytes(AssemblyPath);
            }
            catch (IOException)
            {
                UpdateMainStatus(Colors.Red, $"Failed to read the file {AssemblyPath}");
                return;
            }

            Log("5");
            UpdateMainStatus(Colors.Orange, $"Injecting {Path.GetFileName(AssemblyPath)}");

            using (Injector injector = new Injector(handle, MonoModule))
            {
                try
                {
                    Log("6");
                    IntPtr asm = injector.Inject(file, "LoU", "Loader", "Load");
                    UpdateMainStatus(Colors.Green, $"Injection on {ProcessId.ToString()} successful");
                }
                catch (InjectorException ie)
                {
                    UpdateMainStatus(Colors.Red, $"Injection on {ProcessId.ToString()} failed: {ie.Message}");
                }
                catch (Exception e)
                {
                    UpdateMainStatus(Colors.Red, $"Injection on {ProcessId.ToString()} failed: {e.Message}");
                }
            }
        }
        #endregion Injection

        #region Commands
        private static RoutedCommand connectToLoAClientCommand = new RoutedCommand();
        public static RoutedCommand ConnectToLoAClientCommand { get => connectToLoAClientCommand; set => connectToLoAClientCommand = value; }

        private static RoutedCommand generateMapCommand = new RoutedCommand();
        public static RoutedCommand GenerateMapCommand { get => generateMapCommand; set => generateMapCommand = value; }

        private static RoutedCommand trackPlayerCommand = new RoutedCommand();
        public static RoutedCommand TrackPlayerCommand { get => trackPlayerCommand; set => trackPlayerCommand = value; }

        private static RoutedCommand linkControlsCommand = new RoutedCommand();
        private AssetsManager assetsManager;

        public static RoutedCommand LinkControlsCommand { get => linkControlsCommand; set => linkControlsCommand = value; }

        private void ExitCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private bool CheckForUnsavedChanges()
        {
            if (MessageBoxEx.Show(TheMainWindow, "Are you sure you want to exit?", "Exit", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return false;
            }

            return true;
        }
        private void ExitCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (CheckForUnsavedChanges())
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void ConnectToLoAClientCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void ConnectToLoAClient(int ProcessId)
        {
            UpdateMainStatus(Colors.Orange, $"Connecting to {ProcessId.ToString()}...");
            MainWindow.CurrentClientProcessId = ProcessId;

            MainWindow.ClientStatusMemoryMapMutexName = "ELOU_CS_MX_" + ProcessId.ToString();
            MainWindow.ClientStatusMemoryMapName = "ELOU_CS_" + ProcessId.ToString();
            MainWindow.ClientStatusMemoryMapSize = 1024 * 1024 * 10;
            MainWindow.ClientStatusMemoryMap = new MemoryMap(MainWindow.ClientStatusMemoryMapName, MainWindow.ClientStatusMemoryMapSize, MainWindow.ClientStatusMemoryMapMutexName);

            MainWindow.ClientCommandsMemoryMapMutexName = "ELOU_CC_MX_" + ProcessId.ToString();
            MainWindow.ClientCommandsMemoryMapName = "ELOU_CC_" + ProcessId.ToString();
            MainWindow.ClientCommandsMemoryMapSize = 1024 * 1024;
            MainWindow.ClientCommandsMemoryMap = new MemoryMap(MainWindow.ClientCommandsMemoryMapName, MainWindow.ClientCommandsMemoryMapSize, MainWindow.ClientCommandsMemoryMapMutexName);

            if (MainWindow.ClientStatusMemoryMap.OpenExisting() && MainWindow.ClientCommandsMemoryMap.OpenExisting())
            {
                // Client already patched, memorymaps open already all good
                UpdateMainStatus(Colors.Green, "Connection successful.");
                return;
            }

            if (MessageBoxEx.Show(MainWindow.TheMainWindow, "Game client " + ProcessId.ToString() + " not yet injected. Inject now?", "Game client not yet injected", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            {
                UpdateMainStatus(Colors.Black, "Connection to Legends of Aria game client aborted.");
                return;
            }

            UpdateMainStatus(Colors.Orange, "Connecting to Legends of Aria game client, please wait ...");

            Inject(ProcessId);

            System.Threading.Thread.Sleep(1000);

            if (MainWindow.ClientStatusMemoryMap.OpenExisting() && MainWindow.ClientCommandsMemoryMap.OpenExisting())
            {
                // Client already patched, memorymaps open already all good
                UpdateMainStatus(Colors.Green, "Connection to Legends of Aria game client successful.");
                return;
            }

            UpdateMainStatus(Colors.Red, "Connection to Legends of Aria game client failed!");
        }
        private void ConnectToLoAClientCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            TargetAriaClientPanel.Visibility = Visibility.Visible;

            MouseEventCallback handler = null;
            handler = (MouseEventType type, int x, int y) => {
                // Restore cursors
                // see https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa
                // and also https://autohotkey.com/board/topic/32608-changing-the-system-cursor/
                MouseHook.SystemParametersInfo(0x57, 0, (IntPtr)0, 0);
                TargetAriaClientPanel.Visibility = Visibility.Hidden;

                // Stop global hook
                MouseHook.HookEnd();
                MouseHook.MouseDown -= handler;

                // Get clicked coord
                MouseHook.POINT p;
                p.x = x;
                p.y = y;
                Debug.WriteLine("Clicked x=" + x.ToString() + " y=" + y.ToString());

                // Get clicked window handler, window title
                IntPtr hWnd = MouseHook.WindowFromPoint(p);
                int WindowTitleLength = MouseHook.GetWindowTextLength(hWnd);
                StringBuilder WindowTitle = new StringBuilder(WindowTitleLength + 1);
                MouseHook.GetWindowText(hWnd, WindowTitle, WindowTitle.Capacity);
                Debug.WriteLine("Clicked handle=" + hWnd.ToString() + " title=" + WindowTitle);

                if (WindowTitle.ToString() != "Legends of Aria")
                {
                    MessageBoxEx.Show(MainWindow.TheMainWindow, "The selected window is not a Legends of Aria game client!");
                    return true;
                }

                // Get the processId, and connect
                uint processId;
                MouseHook.GetWindowThreadProcessId(hWnd, out processId);
                Debug.WriteLine("Clicked pid=" + processId.ToString());

                // Attempt connection (or injection, if needed)
                ConnectToLoAClient((int)processId);
                return true;
            };

            //// Prepare cursor image
            //System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            //System.Drawing.Bitmap image = ((System.Drawing.Bitmap)(resources.GetObject("connectToClientToolStripMenuItem.Image")));

            ////// Set all cursors
            ////// see https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setsystemcursor
            ////// and also https://autohotkey.com/board/topic/32608-changing-the-system-cursor/
            //Cursor cursor = new Cursor(image.GetHicon());
            //uint[] cursors = new uint[] { 32512, 32513, 32514, 32515, 32516, 32640, 32641, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650, 32651 };
            //foreach (uint i in cursors)
            //{
            //    MouseHook.SetSystemCursor(cursor.Handle, i);
            //}

            // Start mouse global hook
            MouseHook.MouseDown += handler;
            MouseHook.HookStart();
        }

        private void LinkControlsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void LinkControlsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel controlPanel = new ControlPanel();
            controlPanel.Owner = this;
            controlPanel.ShowDialog();
        }

        private void TrackPlayerCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void TrackPlayerCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel.TrackPlayer = !TrackPlayerMenu.IsChecked;
            TrackPlayerMenu.IsChecked = ControlPanel.TrackPlayer;
            ControlPanel.SaveSettings();
        }

        private void GenerateMapCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void GenerateMapCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                FileName = "resources.assets*",
                Filter = "Unity Assets Files|*.assets"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                Debug.WriteLine(openFileDialog.FileName);
                assetsManager.LoadFiles(openFileDialog.FileNames);
                Debug.WriteLine("Loaded");
                BuildAssetData();
            }
        }

        private string BuildAssetData()
        {
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {

                    if (asset.type == ClassIDType.Texture2D)
                    {

                        Texture2D TextureAsset = (Texture2D)asset;
                        if (TextureAsset.m_Name.StartsWith("Grid_"))
                        {
                            string status = "Exporting: " + TextureAsset.m_Name;
                            if (Exporter.ExportTexture2D(TextureAsset))
                            {
                                Debug.WriteLine("Exported: " + TextureAsset.m_Name);
                            }
                            else
                            {
                                Debug.WriteLine("Export failed for: " + TextureAsset.m_Name);
                            }
                        }

                    }
                }
            }
            Debug.WriteLine("Done");
            return "done";
        }

        #endregion Commands
    }
}
