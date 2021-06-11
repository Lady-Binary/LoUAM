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

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string MINIMUM_LOU_VERSION = "1.3.0.0";

        public static MainWindow TheMainWindow;

        private static Server theServer;
        public static Server TheServer { get => theServer; set => theServer = value; }

        private static Client theClient;
        public static Client TheClient { get => theClient; set => theClient = value; }

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
        DispatcherTimer TimerRefreshCurrentPlayer;

        DispatcherTimer TimerUpdateServer;

        public MainWindow()
        {
            MainWindow.TheMainWindow = this;

            TimerRefreshCurrentPlayer = new DispatcherTimer();
            TimerRefreshCurrentPlayer.Tick += TimerRefreshCurrentPlayer_Tick;
            TimerRefreshCurrentPlayer.Interval = new TimeSpan(0, 0, 0, 0, 100);

            TimerUpdateServer = new DispatcherTimer();
            TimerUpdateServer.Tick += TimerUpdateServer_TickAsync;
            TimerUpdateServer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            TimerUpdateServer.Start();

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

            var servers = Enum.GetValues(typeof(MarkerServerEnum));
            foreach (var server in servers)
            {
                MenuItem newServerMenuItem = new MenuItem();
                newServerMenuItem.Header = "_" + server;
                newServerMenuItem.Command = MainWindowCustomCommands.MapChangeServerCommand;
                newServerMenuItem.CommandParameter = server;
                if (ServerMenuItem.Items.Count == 0) newServerMenuItem.IsChecked = true;
                ServerMenuItem.Items.Add(newServerMenuItem);
            }
            var regions = Enum.GetValues(typeof(MarkerRegionEnum));
            foreach (var region in regions)
            {
                MenuItem newRegionMenuItem = new MenuItem();
                newRegionMenuItem.Header = "_" + region;
                newRegionMenuItem.Command = MainWindowCustomCommands.MapChangeRegionCommand;
                newRegionMenuItem.CommandParameter = region;
                if (RegionMenuITem.Items.Count == 0) newRegionMenuItem.IsChecked = true;
                RegionMenuITem.Items.Add(newRegionMenuItem);
            }

            ChangeRegion(MarkerRegionEnum.Unknown);
            ChangeServer(MarkerServerEnum.Unknown);

            TrackPlayerMenu.IsChecked = ControlPanel.TrackPlayer;
            ControlPanel.LoadPlaces();
            ControlPanel.SavePlaces();
            UpdatePlaces();

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
            MainMap.UpdateAllMarkersOfType(MarkerType.Place, ControlPanel.Places);
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

        public delegate void ChangeServerDelegate(MarkerServerEnum server);
        public void ChangeServer(MarkerServerEnum server)
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

        public delegate void ChangeRegionDelegate(MarkerRegionEnum region);
        public void ChangeRegion(MarkerRegionEnum region)
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
            UpdatePlaces();
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
        private Player GetCurrentPlayer()
        {
            //MockPlayer.LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            //return MockPlayer;

            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
                return null;

            Player currentPlayer = null;

            RefreshClientStatus();

            if (ClientStatus == null)
                return null;

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
                        ClientStatus.CharacterInfo.CHARPOSZ ?? 0,
                        ClientStatus.CharacterInfo.REGION,
                        ClientStatus.ClientInfo.SERVER
                        );

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

            return currentPlayer;
        }

        public bool CheckVersion()
        {
            if (CurrentClientProcessId == -1 || ClientStatusMemoryMap == null)
                return false;

            RefreshClientStatus();

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

        public bool CheckMapData()
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
                    var regions = Enum.GetNames(typeof(MarkerRegionEnum));
                    foreach (var region in regions)
                    {
                        if (region == "Unknown")
                            continue;

                        MapGenerator mapGenerator = new MapGenerator(region);
                        mapGenerator.Owner = TheMainWindow;
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

        public bool CheckMapData(string region)
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
                    mapGenerator.Owner = TheMainWindow;
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

        public void RefreshCurrentPlayer()
        {
            //Debug.Print("RefreshCurrentPlayer");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Player currentPlayer = GetCurrentPlayer();

            if (currentPlayer != null)
            {
                if (CurrentServer != currentPlayer.Server)
                {
                    // Handle server change
                    CurrentServer = currentPlayer.Server;
                    if (ControlPanel.TrackPlayer || MainMap.CurrentServer == MarkerServerEnum.Unknown)
                    {
                        ChangeServer(Marker.URLToServer(CurrentServer));
                    }
                }
                if (CurrentRegion != currentPlayer.Region)
                {
                    // Handle region change
                    CurrentRegion = currentPlayer.Region;
                    if (ControlPanel.TrackPlayer || MainMap.CurrentRegion == MarkerRegionEnum.Unknown)
                    {
                        ChangeRegion(Marker.StringToRegion(CurrentRegion));
                    }
                }
                Marker currentPlayerMarker = new Marker(
                    MarkerFileEnum.None,
                    currentPlayer.Server != "" ? Marker.URLToServer(currentPlayer.Server) : MarkerServerEnum.Unknown,
                    currentPlayer.Region != "" && Enum.TryParse<MarkerRegionEnum>(currentPlayer.Region, out MarkerRegionEnum markerRegion) ? markerRegion : MarkerRegionEnum.Unknown,
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
                return;
            }

            Player currentPlayer = GetCurrentPlayer();
            if (currentPlayer != null && ControlPanel.MyName.Trim() != "" && ControlPanel.MyName.Trim() != "(your name)")
            {
                currentPlayer.DisplayName = ControlPanel.MyName;
            }

            if (TheServer != null)
            {
                UpdateLinkStatus(Colors.Blue, $"LoUAM Link Server listening on {(ControlPanel.Https ? "HTTPS" : "HTTP")} port {ControlPanel.Port} with {(string.IsNullOrEmpty(ControlPanel.Password) ? "no password" : "password")}.");
                lock (TheServer.PlayersLock)
                {
                    if (TheServer.Players != null)
                    {
                        IEnumerable<Player> OtherPlayers = TheServer.Players.Values;
                        if (currentPlayer != null)
                        {
                            TheServer.Players[currentPlayer.ObjectId] = currentPlayer;
                            OtherPlayers = OtherPlayers.Where(player => player.ObjectId != currentPlayer.ObjectId);
                        }
                        List<Marker> OtherMarkers = OtherPlayers?.Select(player =>
                                new Marker(
                                    MarkerFileEnum.None,
                                    Marker.URLToServer(player.Server),
                                    (MarkerRegionEnum)Enum.Parse(typeof(MarkerRegionEnum), player.Region, true),
                                    MarkerType.OtherPlayer,
                                    player.ObjectId.ToString(),
                                    MarkerIcon.none,
                                    player.DisplayName,
                                    player.X,
                                    player.Y,
                                    player.Z
                                    )
                                ).ToList();
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
                        UpdateLinkStatus(Colors.Orange, $"LoUAM Link connecting (attempt {TheClient.ConnectionAttempts + 1})...");
                        await TheClient.ConnectAsync();
                        if (TheClient.ClientState != Client.ClientStateEnum.Connected)
                        {
                            UpdateLinkStatus(Colors.Red, "LoUAM Link disconnected, could not connect!");
                            TheClient.Disconnect();
                            return;
                        }
                    }
                    catch (TooManyAttemptsException ex)
                    {
                        UpdateLinkStatus(Colors.Red, $"LoUAM Link disconnected: {ex.Message}");
                        TheClient.Disconnect();
                        TheClient = null;
                        return;
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
                        await TheClient.UpdatePlayer(currentPlayer);
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
                    if (currentPlayer != null)
                    {
                        OtherPlayers = OtherPlayers.Where(player => player.ObjectId != currentPlayer.ObjectId);
                    }
                    List<Marker> OtherMarkers = OtherPlayers
                        .Select(player => new Marker(
                            MarkerFileEnum.None,
                            Marker.URLToServer(player.Server),
                            (MarkerRegionEnum)Enum.Parse(typeof(MarkerRegionEnum), player.Region, true),
                            MarkerType.OtherPlayer,
                            player.ObjectId.ToString(),
                            MarkerIcon.none,
                            player.DisplayName,
                            player.X,
                            player.Y,
                            player.Z
                            )
                        ).ToList();
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

            if (MessageBoxEx.Show(MainWindow.TheMainWindow, "Game client " + ProcessId.ToString() + " not yet injected. Inject now?", "Game client not yet injected", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
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
            TimerRefreshCurrentPlayer.Stop();

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
                    MessageBoxEx.Show(MainWindow.TheMainWindow, "The selected window is not a Legends of Aria game client!");
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
                    TimerRefreshCurrentPlayer.Start();
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
            ChangeServer(e.Parameter is MarkerServerEnum ? (MarkerServerEnum)e.Parameter : MarkerServerEnum.Unknown);
        }

        private void MapChangeRegionCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void MapChangeRegionCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ChangeRegion(e.Parameter is MarkerRegionEnum ? (MarkerRegionEnum)e.Parameter : MarkerRegionEnum.Unknown);
        }

        private void LinkControlsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void LinkControlsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ControlPanel controlPanel = new ControlPanel(ControlPanel.Tab.LinkControl);
            controlPanel.Owner = this;
            controlPanel.ShowDialog();
            UpdatePlaces();
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

        private void MoveCursorHereCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void MoveCursorHereCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MainMap.Center(MainMap.LastMouseRightButtonUpCoords.X, MainMap.LastMouseRightButtonUpCoords.Y);
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
        #endregion Commands
    }

    public static class MainWindowCustomCommands
    {
        public static RoutedCommand ConnectToLoAClientCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand EditPlacesCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand MapAdditionalSettingsCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand MapChangeServerCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand MapChangeRegionCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand LinkControlsCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand MoveCursorHereCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand NewPlaceCommand { get; set; } = new RoutedCommand();
        public static RoutedCommand TrackPlayerCommand { get; set; } = new RoutedCommand();
    }
}
