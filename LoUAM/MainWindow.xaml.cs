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
using System.Threading;
using System.IO.Compression;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string MINIMUM_LOU_VERSION = "1.3.0.0";

        //public static MainWindow TheMainWindow;

        public static Link TheLink { get; set; }

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

        private string CurrentServer;
        private string CurrentRegion;
        DispatcherTimer RefreshStatusTimer;

        public MainWindow()
        {
            TheLink = new Link();

            RefreshStatusTimer = new DispatcherTimer();
            RefreshStatusTimer.Tick += RefreshStatusTimer_TickAsync;
            RefreshStatusTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            RefreshStatusTimer.IsEnabled = true;

            this.AddHandler(
                Window.MouseDownEvent,
                new MouseButtonEventHandler(Window_MouseDown),
                true);


            InitializeComponent();

            this.Title = "LoUAM - " + Assembly.GetExecutingAssembly().GetName().Version.ToString();

            try
            {
                var file = ReadDllFromCompressedResources("costura.lou.dll.compressed");
                var assembly = System.Reflection.Assembly.Load(file);
                var assemblyVersion = assembly.GetName().Version;
                MINIMUM_LOU_VERSION = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.MajorRevision}.0";
            }
            catch (Exception ex)
            {
                MINIMUM_LOU_VERSION = "0.0.0.0";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ControlPanel.LoadSettings();
            ControlPanel.SaveSettings();

            var servers = Enum.GetValues(typeof(PlaceServerEnum));
            foreach (var server in servers)
            {
                MenuItem newServerMenuItem = new MenuItem();
                newServerMenuItem.Header = "_" + server;
                newServerMenuItem.Command = MainWindowCustomCommands.MapChangeServerCommand;
                newServerMenuItem.CommandParameter = server;
                if (ServerMenuItem.Items.Count == 0) newServerMenuItem.IsChecked = true;
                ServerMenuItem.Items.Add(newServerMenuItem);
            }
            var regions = Enum.GetValues(typeof(PlaceRegionEnum));
            foreach (var region in regions)
            {
                MenuItem newRegionMenuItem = new MenuItem();
                newRegionMenuItem.Header = "_" + region;
                newRegionMenuItem.Command = MainWindowCustomCommands.MapChangeRegionCommand;
                newRegionMenuItem.CommandParameter = region;
                if (RegionMenuITem.Items.Count == 0) newRegionMenuItem.IsChecked = true;
                RegionMenuITem.Items.Add(newRegionMenuItem);
            }

            TrackPlayerMenu.IsChecked = ControlPanel.TrackPlayer;

            ControlPanel.LoadPlaces();
            ControlPanel.SavePlaces();
            UpdatePlaces();

            RefreshShowIcons();
            RefreshShowLabels();
            RefreshTopMost();
            RefreshTiltMap();
            RefreshNoBorder();

            ChangeRegion(PlaceRegionEnum.Unknown);
            ChangeServer(PlaceServerEnum.Unknown);

            if (!Directory.Exists(Map.MAP_DATA_FOLDER))
            {
                MessageBoxEx.Show(this, "Greetings!!\n\nIt appears that this is the first time you run LoUAM.\n\nEvery time you start LoUAM, you need to connect it to your Legends of Aria client first: login into your server of choice and enter the world, then here on LoUAM click on the LoU Menu -> Connect to LoA game client.\n\nThe first time you connect LoUAM to the client, LoUAM will also generate the necessary map data.\n\nEnjoy!", "Map data not found");
                return;
            }
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

        public delegate void UpdatePlacesDelegate();
        public void UpdatePlaces()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new UpdatePlacesDelegate(UpdatePlaces));
                return;
            }
            MainMap.UpdateAllPlacesOfType(PlaceType.Place, ControlPanel.Places);
            MainMap.UpdateMarker();
        }

        public delegate void RefreshMapTilesDelegate();
        public void RefreshMapTiles()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new RefreshMapTilesDelegate(RefreshMapTiles));
                return;
            }
            MainMap.RefreshMapTiles();
        }

        public delegate void ChangeServerDelegate(PlaceServerEnum server);
        public void ChangeServer(PlaceServerEnum server)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new ChangeServerDelegate(ChangeServer), new object[] { server });
                return;
            }

            foreach (MenuItem ChangeServerMenuItem in ServerMenuItem.Items)
            {
                if (ChangeServerMenuItem.Header.ToString() == "_" + server.ToString())
                {
                    ChangeServerMenuItem.IsChecked = true;
                }
                else
                {
                    ChangeServerMenuItem.IsChecked = false;
                }
            }

            MainMap.CurrentServer = server;
            UpdatePlaces();
        }

        public delegate void ChangeRegionDelegate(PlaceRegionEnum region);
        public void ChangeRegion(PlaceRegionEnum region)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new ChangeRegionDelegate(ChangeRegion), new object[] { region });
                return;
            }

            foreach (MenuItem ChangeRegionMenuItem in RegionMenuITem.Items)
            {
                if (ChangeRegionMenuItem.Header.ToString() == "_" + region.ToString())
                {
                    ChangeRegionMenuItem.IsChecked = true;
                }
                else
                {
                    ChangeRegionMenuItem.IsChecked = false;
                }
            }

            CheckMapData(region.ToString());
            MainMap.CurrentRegion = region;
            CurrentRegion = region.ToString();
            UpdatePlaces();
        }

        public delegate void RefreshTopMostDelegate();
        public void RefreshTopMost()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new RefreshTopMostDelegate(RefreshTopMost));
                return;
            }

            TopMostMenu.IsChecked = ControlPanel.TopMost;
            this.Focus();
            this.Topmost = ControlPanel.TopMost;
        }

        public delegate void RefreshTiltMapDelegate();
        public void RefreshTiltMap()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new RefreshTiltMapDelegate(RefreshTiltMap));
                return;
            }

            TiltMapMenu.IsChecked = ControlPanel.TiltMap;
            TransformGroup transformGroup = MainMap.MapGrid.LayoutTransform as TransformGroup;
            if (transformGroup != null)
            {
                foreach (var transform in transformGroup.Children)
                {
                    if (transform is RotateTransform)
                    {
                        RotateTransform rotateTransform = transform as RotateTransform;
                        if (ControlPanel.TiltMap)
                            rotateTransform.Angle = 45;
                        else
                            rotateTransform.Angle = 0;
                    }
                }
            }
            UpdatePlaces();
        }

        public delegate void RefreshNoBorderDelegate();
        public void RefreshNoBorder()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new RefreshNoBorderDelegate(RefreshNoBorder));
                return;
            }


            if (ControlPanel.NoBorder)
            {
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.MainMenu.Visibility = Visibility.Collapsed;
                this.MainMap.CanScroll(false);
                this.MainStatusDockPanel.Visibility = Visibility.Collapsed;
                this.LinkStatusDockPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ResizeMode = ResizeMode.CanResize;
                this.MainMenu.Visibility = Visibility.Visible;
                this.MainMap.CanScroll(true);
                this.MainStatusDockPanel.Visibility = Visibility.Visible;
                this.LinkStatusDockPanel.Visibility = Visibility.Visible;
            }
        }

        #region Timers
        public static int ExecuteCommandAsync(ClientCommand command)
        {
            if (CurrentClientProcessId == -1 || ClientCommandsMemoryMap == null)
                return -1;

            int ClientCommandId = 0;
            Queue<ClientCommand> ClientCommandsQueue;
            ClientCommand[] ClientCommandsArray;
            ClientCommandsMemoryMap.ReadMemoryMap(out ClientCommandId, out ClientCommandsArray);
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

            ClientCommandsQueue.Enqueue(command);
            int AssignedClientCommandId = ClientCommandId + ClientCommandsQueue.Count;
            ClientCommandsMemoryMap.WriteMemoryMap(ClientCommandId, ClientCommandsQueue.ToArray());
            Debug.WriteLine("Command inserted, assigned CommandId=" + AssignedClientCommandId.ToString());

            return AssignedClientCommandId;
        }
        public static void ExecuteCommand(ClientCommand command)
        {
            ExecuteCommand(command, 10000);
        }
        public static void ExecuteCommand(ClientCommand command, long commandTimeout)
        {
            if (CurrentClientProcessId == -1 || ClientCommandsMemoryMap == null)
                return;

            int AssignedClientCommandId = ExecuteCommandAsync(command);

            int ClientCommandId = 0;
            ClientCommand[] ClientCommandsArray;
            Stopwatch timeout = new Stopwatch();
            timeout.Start();
            while (ClientCommandId < AssignedClientCommandId && timeout.ElapsedMilliseconds < commandTimeout)
            {
                Debug.WriteLine("Waiting for command to be executed, Current CommandId=" + ClientCommandId.ToString() + ", Assigned CommandId=" + AssignedClientCommandId.ToString());
                Thread.Sleep(50);
                ClientCommandsMemoryMap.ReadMemoryMap(out ClientCommandId, out ClientCommandsArray);
            }
            timeout.Stop();
            if (timeout.ElapsedMilliseconds >= 60000)
            {
                Debug.WriteLine("Timed out!");
            }
        }
        public static void RefreshClientStatus()
        {
            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
            {
                ClientStatus = null;
            }

            lock (ClientStatusLock)
            {
                ClientStatusMemoryMap.ReadMemoryMap<ClientStatus>(out ClientStatus);
            }
        }

        //private static Random rnd = new Random();
        //private static Player MockPlayer = new Player(
        //        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        //        (ulong)rnd.Next(1, 1000000),
        //        $"LadyBinary",
        //        (ulong)rnd.Next(1, 1000),
        //        (ulong)rnd.Next(1, 1000),
        //        (ulong)rnd.Next(1, 1000),
        //        "NewCelador",
        //        "cluster1.shardsonline.com:5150"
        //        );
        //private Player GetMockPlayer()
        //{
        //    MockPlayer.LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        //    return MockPlayer;
        //}

        private async Task<Player> GetCurrentPlayerAsync()
        {
            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
            {
                this.Title = "LoUAM - " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
                return null;
            }

            Player currentPlayer = null;

            RefreshClientStatus();

            if (ClientStatus == null)
            {
                this.Title = "LoUAM - " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
                return null;
            }

            if (new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - ClientStatus.TimeStamp <= 10000)
            {
                UpdateMainStatus(Colors.Green, $"Connected to Legends of Aria game client {MainWindow.CurrentClientProcessId.ToString()}.");

                if (ClientStatus.CharacterInfo.CHARID != null)
                {
                    var towns = ControlPanel.Places
                        .Where(place => place.Icon == PlaceIcon.town)
                        .OrderBy(town => Math.Sqrt(Math.Pow((town.X - ClientStatus.CharacterInfo.CHARPOSX ?? 0), 2) + Math.Pow((town.Z - ClientStatus.CharacterInfo.CHARPOSZ ?? 0), 2)));
                    string nearestTown = towns.FirstOrDefault()?.Label ?? "";

                    currentPlayer = new Player(
                        ClientStatus.TimeStamp,
                        ClientStatus.CharacterInfo.CHARID ?? 0,
                        TheLink.State != Link.StateEnum.Idle ? ControlPanel.MyName : ClientStatus.CharacterInfo.CHARNAME,
                        ClientStatus.CharacterInfo.CHARPOSX ?? 0,
                        ClientStatus.CharacterInfo.CHARPOSY ?? 0,
                        ClientStatus.CharacterInfo.CHARPOSZ ?? 0,
                        nearestTown,
                        ClientStatus.CharacterInfo.REGION,
                        ClientStatus.ClientInfo.SERVER
                        );

                    Regex rx = new Regex(@"\[(.*?)\]");

                    // Try to extract the color
                    String CharColor = rx.Match(currentPlayer.DisplayName).Value;

                    // Clean up the name
                    String CharName = rx.Replace(currentPlayer.DisplayName, "");

                    currentPlayer.DisplayName = CharName;
                    this.Title = "LoUAM - " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " - " + currentPlayer.DisplayName;
                }
            }
            else
            {
                UpdateMainStatus(Colors.Red, $"Client {MainWindow.CurrentClientProcessId.ToString()} not responding!");
                this.Title = "LoUAM - " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
                return null;
            }

            return currentPlayer;
        }

        private bool CheckVersion()
        {
            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
                return false;

            RefreshClientStatus();

            if (ClientStatus.ClientInfo.LOUVER == null)
            {
                MessageBox.Show("This Legends of Aria client was not properly injected.\n" +
                    "\n" +
                    "Please close and restart both the Legends of Aria client and LoUAM and re-inject the client.");
                return false;
            }

            if (new Version(ClientStatus.ClientInfo.LOUVER) < new Version(MINIMUM_LOU_VERSION))
            {
                MessageBox.Show("This Legends of Aria client was injected with an unsupported version of LoU.dll: " +
                    "as a result, LoUAM may or may not work as expected.\n" +
                    "\n" +
                    "Please close and restart both the Legends of Aria client and LoUAM and re-inject the client.\n" +
                    "\n" +
                    "If you are using EasyLoU and LoUAM simultaneously, please make sure they are both up-to-date.\n" +
                    "\n" +
                    "The latest versions of EasyLoU and LoUAM can be found at:" +
                    "\n" +
                    "https://github.com/Lady-Binary/");
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool CheckMapData()
        {
            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
                return false;

            bool MapDataExported = false;

            if (!Directory.Exists(Map.MAP_DATA_FOLDER))
                Directory.CreateDirectory(Map.MAP_DATA_FOLDER);

            if (Directory.GetDirectories(Map.MAP_DATA_FOLDER).Count() == 0)
            {
                if (MessageBoxEx.Show(this, $"It appears that this is the first time you run LoUAM.\n\nLoUAM will now extract the map images from the Legends of Aria Client: this operation is required and might take several minutes, depending on your computer.\n\nYour Legends of Aria client will become unresponsive while the export is in progress, so please make sure your character is in a safe spot.\n\nClick OK when ready to continue.", $"Map data not present", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    var regions = Enum.GetNames(typeof(PlaceRegionEnum));
                    foreach (var region in regions)
                    {
                        if (region == "Unknown")
                            continue;

                        MapGenerator mapGenerator = new MapGenerator(region);
                        mapGenerator.Owner = this;
                        bool exportSuccessful = mapGenerator.ShowDialog() ?? false;
                        if (!exportSuccessful)
                        {
                            MessageBoxEx.Show(this, $"LoUAM was unable to load the map data for region {region} from the Legends of Aria Client. Please make sure you are using the latest version of LoUAM.", $"Could not load map data for region {region}");
                        }
                        else
                        {
                            MapDataExported = true;
                        }
                    }
                }
            }

            return MapDataExported;
        }
        private bool CheckMapData(string region)
        {
            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
                return false;

            if (region == "Unknown")
                return false;

            if (!Directory.Exists($"{Map.MAP_DATA_FOLDER}"))
                Directory.CreateDirectory($"{Map.MAP_DATA_FOLDER}");

            if (!Directory.Exists($"{Map.MAP_DATA_FOLDER}/{region}"))
                Directory.CreateDirectory($"{Map.MAP_DATA_FOLDER }/{region}");

            if (Directory.GetFiles($"{Map.MAP_DATA_FOLDER}/{region}", "*.json").Count() == 0 ||
                Directory.GetFiles($"{Map.MAP_DATA_FOLDER}/{region}", "*.jpg").Count() == 0 ||
                Directory.GetFiles($"{Map.MAP_DATA_FOLDER}/{region}", "*.json").Count() != Directory.GetFiles($"{Map.MAP_DATA_FOLDER}/{region}", "*.jpg").Count())
            {
                foreach (string f in Directory.EnumerateFiles($"{Map.MAP_DATA_FOLDER }/{region}", "*.*"))
                    File.Delete(f);

                if (MessageBoxEx.Show(this, $"It appears that the map data for the current region ({region}) is outdated.\n\nLoUAM will now extract the map images from the Legends of Aria Client: this operation is required and might take several minutes, depending on your computer.\n\nYour Legends of Aria client will become unresponsive while the export is in progress, so please make sure your character is in a safe spot.\n\nClick OK when ready to continue.", $"Map data outdated for region {region}", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    MapGenerator mapGenerator = new MapGenerator(region.ToString());
                    mapGenerator.Owner = this;
                    if (mapGenerator.ShowDialog() ?? false)
                    {
                        return true;
                    }
                    else
                    {
                        MessageBoxEx.Show(this, $"LoUAM was unable to load the map data for region {region} from the Legends of Aria Client. Please make sure you are using the latest version of LoUAM.", $"Could not load map data for region {region}");
                        return false;
                    }
                }
            }

            return false;
        }

        private async Task RefreshCurrentPlayerStatusAsync()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Player currentPlayer = await GetCurrentPlayerAsync();

            if (currentPlayer != null)
            {
                if (ControlPanel.MyName.Trim() != "" && ControlPanel.MyName.Trim() != "(your name)")
                {
                    // Enforce display name, if set
                    currentPlayer.DisplayName = ControlPanel.MyName;
                }

                // Refresh the current player on link
                await TheLink.CurrentPlayerSemaphoreSlim.WaitAsync();
                try
                {
                    TheLink.CurrentPlayer = currentPlayer;
                }
                finally
                {
                    TheLink.CurrentPlayerSemaphoreSlim.Release();
                }

                if (CurrentServer != currentPlayer.Server)
                {
                    // Handle server change
                    CurrentServer = currentPlayer.Server;
                    if (ControlPanel.TrackPlayer || MainMap.CurrentServer == PlaceServerEnum.Unknown)
                    {
                        ChangeServer(Place.URLToServer(CurrentServer));
                    }
                }
                if (CurrentRegion != currentPlayer.Region)
                {
                    // Handle region change
                    CurrentRegion = currentPlayer.Region;
                    if (ControlPanel.TrackPlayer || MainMap.CurrentRegion == PlaceRegionEnum.Unknown)
                    {
                        ChangeRegion(Place.StringToRegion(CurrentRegion));
                    }
                }
                Place currentPlayerPlace = new Place(
                    PlaceFileEnum.None,
                    currentPlayer.Server != "" ? Place.URLToServer(currentPlayer.Server) : PlaceServerEnum.Unknown,
                    currentPlayer.Region != "" && Enum.TryParse<PlaceRegionEnum>(currentPlayer.Region, out PlaceRegionEnum placeRegion) ? placeRegion : PlaceRegionEnum.Unknown,
                    PlaceType.CurrentPlayer,
                    currentPlayer.ObjectId.ToString(),
                    PlaceIcon.none,
                    currentPlayer.DisplayName,
                    currentPlayer.X,
                    currentPlayer.Y,
                    currentPlayer.Z
                    );
                MainMap.UpdateAllPlacesOfType(PlaceType.CurrentPlayer, new[] { currentPlayerPlace });

                if (ControlPanel.TrackPlayer)
                {
                    if (ControlPanel.TrackPlayerObjectId == 0 || ControlPanel.TrackPlayerObjectId == currentPlayer.ObjectId)
                    {
                        // Center on ourself
                        MainMap.Center(currentPlayerPlace.X, currentPlayerPlace.Z);
                    }
                    else
                    {
                        // Center on some other player
                        Player PlayerToTrack = null;
                        await MainWindow.TheLink.OtherPlayersSemaphoreSlim.WaitAsync();
                        try
                        {
                            PlayerToTrack = MainWindow.TheLink.OtherPlayers?.Where(player => player?.ObjectId == ControlPanel.TrackPlayerObjectId).FirstOrDefault();
                        }
                        finally
                        {
                            MainWindow.TheLink.OtherPlayersSemaphoreSlim.Release();
                        }
                        if (PlayerToTrack != null)
                        {
                            MainMap.Center(PlayerToTrack.X, PlayerToTrack.Z);
                        }
                        else
                        {
                            // Player probably disconnected, let's stop tracking them
                            ControlPanel.TrackPlayer = false;
                            ControlPanel.TrackPlayerObjectId = 0;
                            ControlPanel.SaveSettings();
                            RefreshTrackPlayer();
                        }
                    }
                }
            }
            else
            {
                MainMap.RemoveAllPlacesOfType(PlaceType.CurrentPlayer);
                // Refresh the current player on link
                await TheLink.CurrentPlayerSemaphoreSlim.WaitAsync();
                try
                {
                    TheLink.CurrentPlayer = null;
                }
                finally
                {
                    TheLink.CurrentPlayerSemaphoreSlim.Release();
                }
            }
        }
        private async Task RefreshLinkStatusAsync()
        {
            switch (TheLink.State)
            {
                case Link.StateEnum.Idle:
                    {
                        UpdateLinkStatus(Colors.Black, $"LoUAM Link not connected.");
                    }
                    break;

                case Link.StateEnum.ServerListening:
                    {
                        UpdateLinkStatus(Colors.Blue, $"LoUAM Link Server listening on {(ControlPanel.Https ? "HTTPS" : "HTTP")} port {ControlPanel.Port} with {(string.IsNullOrEmpty(ControlPanel.Password) ? "no password" : "password")}.");
                    }
                    break;

                case Link.StateEnum.ServerListenFailed:
                    {
                        if (TheLink.Error != "")
                            UpdateLinkStatus(Colors.Red, $"LoUAM Link Server on {(ControlPanel.Https ? "HTTPS" : "HTTP")} port {ControlPanel.Port} with {(string.IsNullOrEmpty(ControlPanel.Password) ? "no password" : "password")} failed: {TheLink.Error}.");
                        else
                            UpdateLinkStatus(Colors.Red, $"LoUAM Link Server on {(ControlPanel.Https ? "HTTPS" : "HTTP")} port {ControlPanel.Port} with {(string.IsNullOrEmpty(ControlPanel.Password) ? "no password" : "password")} failed.");
                    }
                    break;

                case Link.StateEnum.ClientDisconnected:
                    {
                        if (TheLink.Error != "")
                            UpdateLinkStatus(Colors.Red, $"LoUAM Link disconnected: {TheLink.Error}");
                        else
                            UpdateLinkStatus(Colors.Red, $"LoUAM Link disconnected.");
                    }
                    break;

                case Link.StateEnum.ClientConnecting:
                    {
                        UpdateLinkStatus(Colors.Orange, $"LoUAM Link connecting (attempt {TheLink.ConnectionAttempts})...");
                    }
                    break;

                case Link.StateEnum.ClientConnected:
                    {
                        UpdateLinkStatus(Colors.Green, $"LoUAM Link connected to {ControlPanel.Host} on port {ControlPanel.Port}");
                    }
                    break;

                case Link.StateEnum.ClientConnectionFailed:
                    {
                        UpdateLinkStatus(Colors.Red, $"LoUAM Link connection to {ControlPanel.Host} on port {ControlPanel.Port} failed: {TheLink.Error}");
                    }
                    break;
            }

            if (TheLink.State == Link.StateEnum.ClientConnected ||
                TheLink.State == Link.StateEnum.ServerListening)
            {
                await TheLink.OtherPlayersSemaphoreSlim.WaitAsync();
                try
                {
                    List<Place> OtherPlaces = TheLink.OtherPlayers
                        .Where(player => player != null)
                        .Select(player => new Place(
                            PlaceFileEnum.None,
                            player.Server != "" ? Place.URLToServer(player.Server) : PlaceServerEnum.Unknown,
                            player.Region != "" && Enum.TryParse<PlaceRegionEnum>(player.Region, out PlaceRegionEnum placeRegion) ? placeRegion : PlaceRegionEnum.Unknown,
                            PlaceType.OtherPlayer,
                            player.ObjectId.ToString(),
                            PlaceIcon.none,
                            player.DisplayName,
                            player.X,
                            player.Y,
                            player.Z
                            )
                        ).ToList();
                    MainMap.UpdateAllPlacesOfType(PlaceType.OtherPlayer, OtherPlaces);
                }
                finally
                {
                    TheLink.OtherPlayersSemaphoreSlim.Release();
                }
            }
        }
        private async void RefreshStatusTimer_TickAsync(object sender, EventArgs e)
        {
            RefreshStatusTimer.Stop();

            await RefreshCurrentPlayerStatusAsync();
            await RefreshLinkStatusAsync();

            RefreshStatusTimer.Start();
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

        private byte[] ReadDllFromFile(string AssemblyPath)
        {
            File.ReadAllBytes(AssemblyPath);
            return null;
        }
        private byte[] ReadDllFromCompressedResources(string ResourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var compressedStream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (compressedStream != null)
                {
                    using (var decompressedStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        if (decompressedStream != null)
                        {
                            decompressedStream.CopyTo(output);
                            return output.ToArray();
                        }
                    }
                }
            }
            return null;
        }
        private void Inject(int ProcessId)
        {
            var MonoModule = GetMonoModule(ProcessId);

            IntPtr handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, ProcessId);

            if (handle == IntPtr.Zero)
            {
                UpdateMainStatus(Colors.Red, "Failed to open process");
                return;
            }

            byte[] file;

            // Once we're happy with hoe Fody.Costura works, we can get rid of this
            //String AssemblyPath = "LoU.dll";
            //try
            //{
            //    file = ReadDllFromFile(AssemblyPath);
            //}
            //catch (IOException)
            //{
            //    UpdateMainStatus(Colors.Red, $"Failed to read the file {AssemblyPath}");
            //    return;
            //}
            //UpdateMainStatus(Colors.Orange, $"Injecting {Path.GetFileName(AssemblyPath)}");

            // New method, where the dll is embedded by Fody.Costura
            String ResourceName = "costura.lou.dll.compressed";
            try
            {
                file = ReadDllFromCompressedResources(ResourceName);
            }
            catch (IOException)
            {
                UpdateMainStatus(Colors.Red, $"Failed to read the resource {ResourceName}");
                return;
            }
            UpdateMainStatus(Colors.Orange, $"Injecting {Path.GetFileName(ResourceName)}");

            using (Injector injector = new Injector(handle, MonoModule))
            {
                try
                {
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
        private void ExitCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private bool CheckForUnsavedChanges()
        {
            if (MessageBoxEx.Show(this, "Are you sure you want to exit?", "Exit", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
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
        public bool ConnectToLoAClient(int ProcessId)
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
                return true;
            }

            if (MessageBoxEx.Show(this, "Game client " + ProcessId.ToString() + " not yet injected. Inject now?", "Game client not yet injected", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            {
                UpdateMainStatus(Colors.Black, "Connection to Legends of Aria game client aborted.");
                return false;
            }

            UpdateMainStatus(Colors.Orange, "Connecting to Legends of Aria game client, please wait ...");

            Inject(ProcessId);

            System.Threading.Thread.Sleep(1000);

            if (MainWindow.ClientStatusMemoryMap.OpenExisting() && MainWindow.ClientCommandsMemoryMap.OpenExisting())
            {
                // Client already patched, memorymaps open already all good
                UpdateMainStatus(Colors.Green, "Connection to Legends of Aria game client successful.");
                return true;
            }

            UpdateMainStatus(Colors.Red, "Connection to Legends of Aria game client failed!");
            return false;
        }
        public void DoConnectToLoAClientCommand()
        {
            TargetAriaClientPanel.Visibility = Visibility.Visible;

            MouseEventCallback handler = null;
            handler = (MouseEventType type, int x, int y) => {
                // Restore cursors
                // see https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa
                // and also https://autohotkey.com/board/topic/32608-changing-the-system-cursor/
                MouseHook.SystemParametersInfo(0x57, 0, (IntPtr)0, 0);

                // Hide message
                TargetAriaClientPanel.Visibility = Visibility.Hidden;

                // Stop global hook
                MouseHook.HookEnd();
                MouseHook.MouseDown -= handler;

                // This is for when scaling is >100%
                double factor = MouseHook.getScalingFactor();

                // Get clicked coord
                MouseHook.POINT p;
                p.x = (int)((double)x / factor);
                p.y = (int)((double)y / factor);
                Debug.WriteLine("Clicked x=" + x.ToString() + " y=" + y.ToString());

                // Get clicked window handler, window title
                IntPtr hWnd = MouseHook.WindowFromPoint(p);
                int WindowTitleLength = MouseHook.GetWindowTextLength(hWnd);
                StringBuilder WindowTitle = new StringBuilder(WindowTitleLength + 1);
                MouseHook.GetWindowText(hWnd, WindowTitle, WindowTitle.Capacity);
                Debug.WriteLine("Clicked handle=" + hWnd.ToString() + " title=" + WindowTitle);

                if (WindowTitle.ToString() != "Legends of Aria")
                {
                    MessageBoxEx.Show(this, "The selected window is not a Legends of Aria game client!");
                    return true;
                }

                // Get the processId, and connect
                uint processId;
                MouseHook.GetWindowThreadProcessId(hWnd, out processId);
                Debug.WriteLine("Clicked pid=" + processId.ToString());

                // Attempt connection (or injection, if needed)
                bool connected = ConnectToLoAClient((int)processId);

                if (connected)
                {
                    CheckVersion();
                    if (CheckMapData())
                    {
                        RefreshMapTiles();
                    }
                }

                return connected;
            };

            // Prepare cursor image
            System.Drawing.Bitmap image = LoUAM.Properties.Resources.uo.ToBitmap();

            // Set all cursors
            // see https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setsystemcursor
            // and also https://autohotkey.com/board/topic/32608-changing-the-system-cursor/
            uint[] cursors = new uint[] { 32512, 32513, 32514, 32515, 32516, 32640, 32641, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650, 32651 };
            foreach (uint i in cursors)
            {
                MouseHook.SetSystemCursor(image.GetHicon(), i);
            }

            // Start mouse global hook
            MouseHook.MouseDown += handler;
            MouseHook.HookStart();
        }
        private void ConnectToLoAClientCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DoConnectToLoAClientCommand();
        }

        private void EditPlacesCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void EditPlacesCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel controlPanel = new ControlPanel(ControlPanel.Tab.Places);
            controlPanel.Owner = this;
            controlPanel.ShowDialog();
            UpdatePlaces();
        }

        private void MapAdditionalSettingsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void MapAdditionalSettingsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel controlPanel = new ControlPanel(ControlPanel.Tab.Map);
            controlPanel.Owner = this;
            controlPanel.ShowDialog();
            UpdatePlaces();
        }

        private void MapChangeServerCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void MapChangeServerCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ChangeServer(e.Parameter is PlaceServerEnum ? (PlaceServerEnum)e.Parameter : PlaceServerEnum.Unknown);
        }

        private void MapChangeRegionCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void MapChangeRegionCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ChangeRegion(e.Parameter is PlaceRegionEnum ? (PlaceRegionEnum)e.Parameter : PlaceRegionEnum.Unknown);
        }

        private void LinkControlsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void LinkControlsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel controlPanel = new ControlPanel(ControlPanel.Tab.LinkControl);
            controlPanel.Owner = this;
            controlPanel.Show();
        }

        private void PlayersListCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void PlayersListCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel controlPanel = new ControlPanel(ControlPanel.Tab.Players);
            controlPanel.Owner = this;
            controlPanel.ShowDialog();
            UpdatePlaces();
        }

        public delegate void RefreshTrackPlayerDelegate();
        public void RefreshTrackPlayer()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new RefreshTrackPlayerDelegate(RefreshTrackPlayer));
                return;
            }
            TrackPlayerMenu.IsChecked = ControlPanel.TrackPlayer;
        }
        private void TrackPlayerCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void TrackPlayerCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel.TrackPlayer = !TrackPlayerMenu.IsChecked;
            ControlPanel.TrackPlayerObjectId = 0;
            ControlPanel.SaveSettings();
            RefreshTrackPlayer();
        }

        private void TopMostCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void TopMost_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel.TopMost = !TopMostMenu.IsChecked;
            ControlPanel.SaveSettings();
            RefreshTopMost();
        }

        private void TiltMapCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void TiltMapCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel.TiltMap = !TiltMapMenu.IsChecked;
            ControlPanel.SaveSettings();
            RefreshTiltMap();
        }

        private void MoveCursorHereCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void MoveCursorHereCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MainMap.Center(MainMap.LastMouseRightButtonUpCoords.X, MainMap.LastMouseRightButtonUpCoords.Y);
        }

        private void DropOrPickupMarkerCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void DropOrPickupMarkerCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MenuItem DropOrPickupMarkerMenuItem = MainMap.ContextMenu.Items[1] as MenuItem;
            if (DropOrPickupMarkerMenuItem.IsChecked)
            {
                RemoveMarker();
            }
            else
            {
                AddMarker(MainMap.LastMouseRightButtonUpCoords.X, 0, MainMap.LastMouseRightButtonUpCoords.Y);
            }
        }
        public void AddMarker(double x, double y, double z)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<double, double, double>(AddMarker), new object[] { x, y, z });
                return;
            }

            MainMap.AddMarker(MainMap.LastMouseRightButtonUpCoords.X, 0, MainMap.LastMouseRightButtonUpCoords.Y);

            MenuItem DropOrPickupMarkerMenuItem = MainMap.ContextMenu.Items[1] as MenuItem;
            if (DropOrPickupMarkerMenuItem == null)
                return;
            DropOrPickupMarkerMenuItem.IsChecked = true;
        }
        public void AddMarker(string id)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<string>(AddMarker), new object[] { id });
                return;
            }

            MainMap.AddMarker(id);

            MenuItem DropOrPickupMarkerMenuItem = MainMap.ContextMenu.Items[1] as MenuItem;
            if (DropOrPickupMarkerMenuItem == null)
                return;
            DropOrPickupMarkerMenuItem.IsChecked = true;
        }
        public void RemoveMarker()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(RemoveMarker));
                return;
            }

            MainMap.RemoveMarker();

            MenuItem DropOrPickupMarkerMenuItem = MainMap.ContextMenu.Items[1] as MenuItem;
            if (DropOrPickupMarkerMenuItem == null)
                return;
            DropOrPickupMarkerMenuItem.IsChecked = false;
        }

        public void RefreshShowLabels()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(RefreshShowLabels));
                return;
            }

            ShowHideLabelsMenu.IsChecked = ControlPanel.ShowLabels;
            UpdatePlaces();
        }
        private void ShowHideLabelsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void ShowHideLabelsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel.ShowLabels = !ShowHideLabelsMenu.IsChecked;
            ControlPanel.SaveSettings();

            RefreshShowLabels();
        }

        public void RefreshShowIcons()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(RefreshShowIcons));
                return;
            }

            ShowHideIconsMenu.IsChecked = ControlPanel.ShowIcons;
            UpdatePlaces();
        }
        private void ShowHideIconsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void ShowHideIconsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel.ShowIcons = !ShowHideIconsMenu.IsChecked;
            ControlPanel.SaveSettings();

            RefreshShowIcons();
        }

        private void NewPlaceCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void NewPlaceCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Point? NewPlaceCoords = e.Parameter as Point?;
            if (NewPlaceCoords != null)
            {
                EditPlace editPlace = new EditPlace(MainMap.CurrentServer, MainMap.CurrentRegion, NewPlaceCoords.Value.X, NewPlaceCoords.Value.Y);
                editPlace.Owner = this;
                editPlace.ShowDialog();
                UpdatePlaces();
            }
        }

        private void CopyLocationCoordintesCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void CopyLocationCoordintesCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Point? LocationCoordinates = e.Parameter as Point?;
            if (LocationCoordinates != null)
            {
                Clipboard.SetText($"{LocationCoordinates.Value.X:0.00} {LocationCoordinates.Value.Y:0.00}");
            }
        }
        #endregion Commands

        // Helper method for dumping the map image to a file
        public void DumpMap()
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)MainMap.TilesCanvas.RenderSize.Width,
                (int)MainMap.TilesCanvas.RenderSize.Height, 96d, 96d, System.Windows.Media.PixelFormats.Default);
            rtb.Render(MainMap.TilesCanvas);

            // Create a render bitmap and push the surface to it
            RenderTargetBitmap renderBitmap =
              new RenderTargetBitmap(
                (int)MainMap.TilesCanvas.Width * 2,
                (int)MainMap.TilesCanvas.Height * 2,
                96d,
                96d,
                PixelFormats.Pbgra32);
            renderBitmap.Render(MainMap.TilesCanvas);

            // Create a file stream for saving image
            using (FileStream outStream = new FileStream("map.png", FileMode.Create))
            {
                // Use png encoder for our data
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                // push the rendered bitmap to it
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                // save the data to the stream
                encoder.Save(outStream);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ControlPanel.NoBorder)
                if (e.ChangedButton == MouseButton.Left)
                    if (e.ButtonState == MouseButtonState.Pressed)
                    {
                        this.DragMove();
                        e.Handled = true;
                        return;
                    }

            e.Handled = false;
        }

        private void MainMap_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ControlPanel.NoBorder)
            {
                ControlPanel.NoBorder = false;
                RefreshNoBorder();

                ControlPanel.TopMost = false;
                RefreshTopMost();

                ControlPanel.SaveSettings();
            }
            else
            {
                ControlPanel.NoBorder = true;
                RefreshNoBorder();

                ControlPanel.TopMost = true;
                RefreshTopMost();

                ControlPanel.SaveSettings();
            }
        }
    }

    public static class MainWindowCustomCommands
    {
        public static RoutedCommand ConnectToLoAClientCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand EditPlacesCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand MoveCursorHereCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand DropOrPickupMarkerCommand { get; set; } = new RoutedCommand();
        // Places
        public static RoutedCommand ShowHideLabelsCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand ShowHideIconsCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand NewPlaceCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand CopyLocationCoordintesCommand { get; set; } = new RoutedCommand();
        // Map
        public static RoutedCommand MapAdditionalSettingsCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand TiltMapCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand TopMostCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand MapChangeServerCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand MapChangeRegionCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand TrackPlayerCommand { get; set; } = new RoutedCommand();
        // Link Controls
        public static RoutedCommand LinkControlsCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand PlayersListCommand { get; set; } = new RoutedCommand();
    }
}
