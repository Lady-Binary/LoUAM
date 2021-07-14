using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
    public partial class ControlPanel : Window
    {
        public enum Tab
        {
            Places = 0,
            Players,
            Map,
            LinkControl
        }

        public static string MyName = "(your name)";
        public static string Host = "";
        public static bool Https = true;
        public static int Port = 4443;
        public static string Password = "";

        public static bool TrackPlayer = true;
        public static ulong TrackPlayerObjectId = 0;

        public static object PlacesLock = new object();
        public static List<Place> Places = new List<Place>();

        // Map
        public static bool TopMost = false;
        public static bool TiltMap = false;
        public static float Brightness = 1;
        public static bool NoBorder = false;

        DispatcherTimer RefreshLinkStatusTimer;
        DispatcherTimer RefreshPlayersTimer;

        public ControlPanel()
        {
            InitializeComponent();
            RefreshLinkStatusTimer = new DispatcherTimer();
            RefreshLinkStatusTimer.Tick += RefreshLinkStatusTimer_Tick;
            RefreshLinkStatusTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            RefreshLinkStatusTimer.Start();
            RefreshPlayersTimer = new DispatcherTimer();
            RefreshPlayersTimer.Tick += RefreshPlayersTimer_TickAsync;
            RefreshPlayersTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            RefreshPlayersTimer.Start();
        }

        public ControlPanel(Tab TabIndex) : this()
        {
            ControlPanelTabControl.SelectedIndex = (int)TabIndex;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MyNameTextBox.Text = MyName;
            HostTextBox.Text = Host;
            PortTextBox.Text = Port.ToString();
            PasswordTextBox.Text = Password;
            HttpsCheckBox.IsChecked = Https;

            // Map
            TopMostCheckbox.IsChecked = TopMost;
            TiltMapCheckbox.IsChecked = TiltMap;
            BrightnessSlider.Value = Brightness;

            RefreshPlaces();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MyName = MyNameTextBox.Text;
            Host = HostTextBox.Text;
            Port = int.TryParse(PortTextBox.Text, out int i) ? i : 4443;
            Password = PasswordTextBox.Text;
            Https = HttpsCheckBox.IsChecked ?? true;

            // Map
            TopMost = TopMostCheckbox.IsChecked ?? false;
            TiltMap = TiltMapCheckbox.IsChecked ?? false;
            Brightness = (float)BrightnessSlider.Value;

            SaveSettings();
        }

        private void RefreshLinkStatusTimer_Tick(object sender, EventArgs e)
        {
            if (this.Owner as MainWindow != null)
            {
                MainWindow mainWindow = this.Owner as MainWindow;
                LinkStatus.Content = mainWindow.LinkStatusLabel.Content;
                LinkStatus.Foreground = mainWindow.LinkStatusLabel.Foreground;
            }
            if (MainWindow.TheLinkServer != null || MainWindow.TheLinkClient != null)
            {
                StartServer.IsEnabled = false;
                LinkToServer.IsEnabled = false;
                BreakConnection.IsEnabled = true;
            }
            else
            {
                StartServer.IsEnabled = true;
                LinkToServer.IsEnabled = true;
                BreakConnection.IsEnabled = false;
            }
        }

        private async System.Threading.Tasks.Task RefreshPlayersAsync()
        {
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(RefreshPlayersAsync);
                return;
            }
            IEnumerable<Player> Players = null;
            if (MainWindow.TheLinkClient != null)
            {
                await MainWindow.TheLinkClient.OtherPlayersSemaphoreSlim.WaitAsync();
                await MainWindow.TheLinkClient.CurrentPlayerSemaphoreSlim.WaitAsync();

                try
                {
                    Players = MainWindow.TheLinkClient.OtherPlayers;
                    if (MainWindow.TheLinkClient.CurrentPlayer != null)
                    {
                        Players = Players.Union(Enumerable.Repeat(MainWindow.TheLinkClient.CurrentPlayer, 1));
                    }
                }
                finally
                {
                    MainWindow.TheLinkClient.CurrentPlayerSemaphoreSlim.Release();
                    MainWindow.TheLinkClient.OtherPlayersSemaphoreSlim.Release();
                }
            }
            if (MainWindow.TheLinkServer != null)
            {
                await MainWindow.TheLinkServer.OtherPlayersSemaphoreSlim.WaitAsync();
                await MainWindow.TheLinkServer.CurrentPlayerSemaphoreSlim.WaitAsync();

                try
                {
                    Players = MainWindow.TheLinkServer.OtherPlayers.Values;
                    if (MainWindow.TheLinkServer.CurrentPlayer != null)
                    {
                        Players = Players.Union(Enumerable.Repeat(MainWindow.TheLinkServer.CurrentPlayer, 1));
                    }
                } finally
                {
                    MainWindow.TheLinkServer.CurrentPlayerSemaphoreSlim.Release();
                    MainWindow.TheLinkServer.OtherPlayersSemaphoreSlim.Release();
                }
            }
            if (Players != null)
            {
                Player SelectedPlayer = PlayersListView.SelectedItem as Player;
                PlayersListView.ItemsSource = Players.Where(player => player != null).OrderBy(player => player.DisplayName);
                if (SelectedPlayer != null)
                {
                    PlayersListView.SelectedItem = Players.Where(player => player != null && player.ObjectId == SelectedPlayer.ObjectId).FirstOrDefault();
                }
            } else
            {
                PlayersListView.ItemsSource = null;
            }
        }

        private async void RefreshPlayersTimer_TickAsync(object sender, EventArgs e)
        {
            RefreshPlayersTimer.Stop();

            await RefreshPlayersAsync();

            RefreshPlayersTimer.Start();
        }

        private void PortTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex _regex = new Regex("[^0-9.-]+");
            e.Handled = _regex.IsMatch(e.Text);
        }

        public static void LoadSettings()
        {
            RegistryKey SoftwareKey = Registry.CurrentUser.OpenSubKey("Software", true);

            RegistryKey LoUAMKey = SoftwareKey.OpenSubKey("LoUAM", true);
            if (LoUAMKey == null)
            {
                LoUAMKey = SoftwareKey.CreateSubKey("LoUAM", true);
            }

            MyName = (string)LoUAMKey.GetValue("MyName", "(your name)");
            Host = (string)LoUAMKey.GetValue("Host", "");
            Port = int.TryParse(LoUAMKey.GetValue("Port", 4443).ToString(), out int i) ? i : 4443;
            Password = (string)LoUAMKey.GetValue("Password", "");
            Https = bool.TryParse(LoUAMKey.GetValue("Https", true).ToString(), out bool https) ? https : true;

            TrackPlayer = bool.TryParse(LoUAMKey.GetValue("TrackPlayer", true).ToString(), out bool trackPlayer) ? trackPlayer : true;
            TrackPlayerObjectId = ulong.TryParse(LoUAMKey.GetValue("TrackPlayerObjectId", 0).ToString(), out ulong trackPlayerObjectId) ? trackPlayerObjectId : 0;

            // Map
            TopMost = bool.TryParse(LoUAMKey.GetValue("TopMost", false).ToString(), out bool topMost) ? topMost : false;
            TiltMap = bool.TryParse(LoUAMKey.GetValue("TiltMap", false).ToString(), out bool tiltMap) ? tiltMap : false;
            Brightness = float.TryParse(LoUAMKey.GetValue("Brightness", 1.0f).ToString(), out float brightness) ? brightness : 1;
            NoBorder = bool.TryParse(LoUAMKey.GetValue("NoBorder", false).ToString(), out bool noBorder) ? noBorder : false;
        }

        public static void SaveSettings()
        {
            RegistryKey SoftwareKey = Registry.CurrentUser.OpenSubKey("Software", true);

            RegistryKey LoUAMKey = SoftwareKey.OpenSubKey("LoUAM", true);
            if (LoUAMKey == null)
            {
                LoUAMKey = SoftwareKey.CreateSubKey("LoUAM", true);
            }

            LoUAMKey.SetValue("MyName", MyName);
            LoUAMKey.SetValue("Host", Host);
            LoUAMKey.SetValue("Port", Port);
            LoUAMKey.SetValue("Password", Password);
            LoUAMKey.SetValue("Https", Https);

            LoUAMKey.SetValue("TrackPlayer", TrackPlayer);
            LoUAMKey.SetValue("TrackPlayerObjectId", TrackPlayerObjectId);

            // Map
            LoUAMKey.SetValue("TopMost", TopMost);
            LoUAMKey.SetValue("TiltMap", TiltMap);
            LoUAMKey.SetValue("Brightness", Brightness);
            LoUAMKey.SetValue("NoBorder", NoBorder);
        }

        // I used this when I dumped the places from https://www.worldanvil.com/w/legends-of-ultima-legendsofultima/map/f830b718-06c1-42cf-a8ed-460269315023
        private static (float, float) WorldAnvilLatLngToWorldLatLng(float lat, float lng)
        {

            float WORLDANVILMAP_NE_LNG = 4000;
            float WORLDANVILMAP_NE_LAT = 4000;
            float WORLDANVILMAP_SW_LNG = 0;
            float WORLDANVILMAP_SW_LAT = 0;

            float WORLD_SW_LAT = -3968.87f;
            float WORLD_SW_LNG = -3741.21f;
            float WORLD_NE_LAT = 3487.13f;
            float WORLD_NE_LNG = 3706.79f;

            float X_FACTOR = (WORLD_NE_LNG - WORLD_SW_LNG) / (WORLDANVILMAP_NE_LNG - WORLDANVILMAP_SW_LNG);
            float Y_FACTOR = (WORLD_NE_LAT - WORLD_SW_LAT) / (WORLDANVILMAP_NE_LAT - WORLDANVILMAP_SW_LAT);

            lng = lng + 235; // there's an offset between the map we're using, and the map worldanvil uses
            lat = lat + 196; // there's an offset between the map we're using, and the map worldanvil uses

            float WORLD_LNG = (lng - WORLDANVILMAP_SW_LNG) * X_FACTOR + WORLD_SW_LNG;
            float WORLD_LAT = (lat - WORLDANVILMAP_SW_LAT) * Y_FACTOR + WORLD_SW_LAT;

            return (WORLD_LAT, WORLD_LNG);
        }

        #region Places
        private delegate void RefreshPlacesDelegate();
        private void RefreshPlaces()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new RefreshPlacesDelegate(RefreshPlaces));
                return;
            }
            PlacesListView.ItemsSource = Places.OrderBy(place => place.Label);
        }

        #endregion
        private static Place LoadPlace(PlaceFileEnum file, PlaceServerEnum server, PlaceRegionEnum region, XmlNode placeNode)
        {
            try
            {
                XmlNode nameNode = placeNode.SelectSingleNode("name");
                string name = nameNode.InnerText;

                XmlNode typeNode = placeNode.SelectSingleNode("type");
                PlaceIcon type = Enum.TryParse<PlaceIcon>(typeNode.InnerText, true, out type) ? type : PlaceIcon.none;

                // Palaces from XML file might come in a different decimal separator,
                // (i.e. if the file has just been extracted or if a friend shared their file),
                // so we are enforcing the decimal separator of the current locale
                string NumberDecimalSeparator = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;

                XmlNode zNode = placeNode.SelectSingleNode("z");
                double z = double.TryParse(zNode.InnerText.Replace(",", NumberDecimalSeparator).Replace(".", NumberDecimalSeparator), out z) ? z : 0;

                XmlNode xNode = placeNode.SelectSingleNode("x");
                double x = double.TryParse(xNode.InnerText.Replace(",", NumberDecimalSeparator).Replace(".", NumberDecimalSeparator), out x) ? x : 0;

                Place place = new Place(file, server, region, PlaceType.Place, Guid.NewGuid().ToString("N"), type, name, x, 0, z);

                return place;
            }
            catch (Exception ex)
            {
                Debug.Print($"Cannot load place: {ex.Message}");
                return null;
            }
        }
        public static void LoadPlaces()
        {
            List<Place> CommonPlaces = LoadPlaces(PlaceFileEnum.Common, "common-places.xml");
            List<Place> PersonalPlaces = LoadPlaces(PlaceFileEnum.Personal, "personal-places.xml");

            Places = CommonPlaces
                    .Union(PersonalPlaces)
                    .ToList();
        }
        public static List<Place> LoadPlaces(PlaceFileEnum file, string fileName)
        {
            List<Place> LoadedPlaces = new List<Place>();

            if (!File.Exists(fileName))
            {
                SavePlaces(fileName, LoadedPlaces);
                return LoadedPlaces;
            }

            XmlDocument doc;
            try
            {
                doc = new XmlDocument();
                doc.Load(fileName);
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show($"Cannot load {fileName}: {ex.Message}");
                return LoadedPlaces;
            }

            foreach(XmlNode ServerNode in doc.DocumentElement.ChildNodes)
            {
                Enum.TryParse(ServerNode.Name, true, out PlaceServerEnum Server);
                foreach (XmlNode RegionNode in ServerNode.ChildNodes)
                {
                    Enum.TryParse(RegionNode.Name, true, out PlaceRegionEnum Region);
                    foreach (XmlNode PlaceNode in RegionNode.ChildNodes)
                    {
                        Place place = LoadPlace(file, Server, Region, PlaceNode);
                        if (place != null)
                        {
                            LoadedPlaces.Add(place);
                        }
                    }
                }
            }

            return LoadedPlaces;
        }
    
        private static Point LatLonToPoint(double Lat, double Lon)
        {
            // project first, taken from SphericalMercator, converts latlon to a point

            double R = 6378137; // earth radius
            double MAX_LATITUDE = 85.0511287798; // maximum latitude

            double d = Math.PI / 180,
                    max = MAX_LATITUDE,
                    lat = Math.Max(Math.Min(max, Lat), -max),
                    sin = Math.Sin(lat * d);

            double point_x = R * Lon * d,
                point_y = R * Math.Log((1 + sin) / (1 - sin)) / 2;

            // then transform, taken from EPSG3395

            double scale = 0.5 / (Math.PI * R);
            double _a = scale, _b = 0.5, _c = -scale, _d = 0.5;

            point_x = _a * point_x + _b;
            point_y = _c * point_y + _d;

            return new Point(point_x, point_y);
        }


        public static List<Place> LoadPlacesFromLoACSV(string fileName)
        {
            // This is only a helper method we've used to import places from the following map
            // https://legendsofaria.gamepedia.com/Celador_Locations

            List<Place> LoadedPlaces = new List<Place>();

            string[] tokens;
            char[] separators = { ',' };
            string str = "";

            FileStream fs = new FileStream(fileName,
                                           FileMode.Open);
            StreamReader sr = new StreamReader(fs, Encoding.Default);

            while ((str = sr.ReadLine()) != null)
            {
                tokens = str.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens[0] == "Location Type")
                    continue;
                string type = tokens[0];
                string name = tokens[1];
                string x = tokens[2];
                string y = tokens[3];

                PlaceIcon _icon = PlaceIcon.point_of_interest;
                switch (type)
                {
                    case "City":
                        _icon = PlaceIcon.town;
                        break;

                    case "Transport":
                        _icon = PlaceIcon.teleporter;
                        break;

                    case "Mine":
                        _icon = PlaceIcon.miners_guild;
                        break;

                    case "Monsters":
                        _icon = PlaceIcon.graveyard;
                        break;

                    case "Dungeons":
                        _icon = PlaceIcon.dungeon;
                        break;

                    case "Taming":
                        _icon = PlaceIcon.point_of_interest;
                        break;
                }

                double _x = double.Parse(x);
                double _z = double.Parse(y);

                Place place = new Place(
                    PlaceFileEnum.Common,
                    PlaceServerEnum.LoA,
                     PlaceRegionEnum.NewCelador,
                      PlaceType.Place,
                     Guid.NewGuid().ToString("N"),
                     _icon,
                     name,
                     _x,
                     0,
                     _z)
                     ;

                LoadedPlaces.Add(place);
            }

            return LoadedPlaces;
        }
        public static List<Place> LoadPlacesFromWorldMapJSON(string fileName)
        {
            // This is only a helper method we've used to import places from the following map
            // https://map.legendsofultima.online/api/markers/get

            List<Place> LoadedPlaces = new List<Place>();

            using (StreamReader r = new StreamReader(fileName))
            {
                string json = r.ReadToEnd();
                dynamic array = JsonConvert.DeserializeObject(json);
                foreach (var item in array)
                {
                    string name = item.label.ToString();
                    string type = item.category.ToString();

                    var position = item.position as Newtonsoft.Json.Linq.JArray;
                    double lat = double.Parse(position[0].ToString());
                    double lon = double.Parse(position[1].ToString());

                    PlaceIcon _icon = PlaceIcon.point_of_interest;

                    if (type == "Cities")
                        _icon = PlaceIcon.town;
                    else if(type == "Dungeons")
                        _icon = PlaceIcon.dungeon;
                    else if (type == "Taming")
                        _icon = PlaceIcon.point_of_interest;
                    else if (type == "Graveyards")
                        _icon = PlaceIcon.graveyard;
                    else if (type == "Harvesting")
                        _icon = PlaceIcon.point_of_interest;
                    else if (type == "Points of Interest")
                        _icon = PlaceIcon.point_of_interest;
                    else if (type == "Player Vendors")
                        _icon = PlaceIcon.point_of_interest;
                    else if (type == "Moongates")
                        _icon = PlaceIcon.moongate;
                    else if (type == "Healers")
                        _icon = PlaceIcon.healer;
                    else if (type == "Moongates")
                        _icon = PlaceIcon.moongate;
                    else if (type == "NPC Vendors")
                    {
                        string _name = name.ToLower();
                        if (_name.Contains("healer"))
                            _icon = PlaceIcon.healer;
                        else if (_name.Contains("tavern"))
                            _icon = PlaceIcon.tavern;
                        else if (_name.Contains("pub"))
                            _icon = PlaceIcon.inn;
                        else if (_name.Contains("blacksmith"))
                            _icon = PlaceIcon.blacksmith;
                        else if (_name.Contains("tanner"))
                            _icon = PlaceIcon.tanner;
                        else if (_name.Contains("tanner"))
                            _icon = PlaceIcon.tanner;
                        else if (_name.Contains("architech"))
                            _icon = PlaceIcon.carpenter;
                        else if (_name.Contains("bard"))
                            _icon = PlaceIcon.bard;
                        else if (_name.Contains("archer"))
                            _icon = PlaceIcon.bowyer;
                        else if (_name.Contains("tailor"))
                            _icon = PlaceIcon.tailor;
                        else if (_name.Contains("weaponsmith"))
                            _icon = PlaceIcon.weapons_guild;
                        else if (_name.Contains("treasure hunter"))
                            _icon = PlaceIcon.traders_guild;
                        else if (_name.Contains("fisherman"))
                            _icon = PlaceIcon.fishermans_guild;
                        else if (_name.Contains("chef"))
                            _icon = PlaceIcon.cooks_guild;
                        else if (_name.Contains("provisioner"))
                            _icon = PlaceIcon.provisioner;
                        else if (_name.Contains("theater"))
                            _icon = PlaceIcon.theater;
                        else if (_name.Contains("inn"))
                            _icon = PlaceIcon.inn;
                        else if (_name.Contains("stable"))
                            _icon = PlaceIcon.stable;
                        else if (_name.Contains("mage"))
                            _icon = PlaceIcon.mage;
                        else if (_name.Contains("scribe"))
                            _icon = PlaceIcon.mage;
                        else if (_name.Contains("tinker"))
                            _icon = PlaceIcon.tinker;
                        else if (_name.Contains("bank"))
                            _icon = PlaceIcon.bank;
                        else if (_name.Contains("woodworker"))
                            _icon = PlaceIcon.carpenter;
                        else if (_name.Contains("carpenter"))
                            _icon = PlaceIcon.carpenter;
                        else if (_name.Contains("butcher"))
                            _icon = PlaceIcon.butcher;
                        else if (_name.Contains("baker"))
                            _icon = PlaceIcon.baker;
                        else if (_name.Contains("smith"))
                            _icon = PlaceIcon.blacksmith;
                        else if (_name.Contains("jeweler"))
                            _icon = PlaceIcon.jeweler;
                        else if (_name.Contains("jeweller"))
                            _icon = PlaceIcon.jeweler;
                        else if (_name.Contains("thief"))
                            _icon = PlaceIcon.thieves_guild;
                        else if (_name.Contains("fletch"))
                            _icon = PlaceIcon.fletcher;
                        else if (_name.Contains("cartographer"))
                            _icon = PlaceIcon.shipwright;
                        else
                            _icon = PlaceIcon.point_of_interest;
                    }

                    // First, we need to convert from LatLon to a pixel on the map

                    // Let's calculate boundaries first, so that we can then scale it
                    var sw = LatLonToPoint(0, 0); // South West coordinate
                    var ne = LatLonToPoint(100, 200); // North East coordinate

                    // Now let's get the actual point
                    var point = LatLonToPoint(lat, lon);

                    // Actual point scaled to the actual map image of 6144x6144
                    var x = (point.X - sw.X) * (6144 / (ne.X - sw.X)) - (6144 / 2);
                    var z = (point.Y - sw.Y) * (6144 / (ne.Y - sw.Y)) - (6144 / 2);

                    // Finally, the map is bit scaled
                    // PNG on that website is originally a 6000x6000 stretched to 6144x6144,
                    // while our world is 6500x6500 and perfectly consistent with the world map
                    ///
                    // In order to perfectly overlap it with the world map,
                    // The coords need to be scaled to 6000x6000 
                    double XScale = 6000 / 6144.0;
                    double YScale = 6000 / 6144.0;
                    x = (x * XScale);
                    z = (z * YScale);

                    Place place = new Place(
                        PlaceFileEnum.Common,
                        PlaceServerEnum.LoU,
                        PlaceRegionEnum.britanniamain,
                        PlaceType.Place,
                        Guid.NewGuid().ToString("N"),
                        _icon,
                        name,
                        x,
                        0,
                        z);

                    LoadedPlaces.Add(place);
                }
            }

            return LoadedPlaces;
        }

        public static void SavePlaces()
        {
            List<Place> CommonPlaces = Places.Where(place => place.File == PlaceFileEnum.Common).ToList();
            SavePlaces("common-places.xml", CommonPlaces);

            List<Place> PersonalPlaces = Places.Where(place => place.File == PlaceFileEnum.Personal).ToList();
            SavePlaces("personal-places.xml", PersonalPlaces);
        }
        public static void SavePlaces(string fileName, List<Place> places)
        {
            XmlDocument doc;
            doc = new XmlDocument();

            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            XmlElement placesNode = doc.CreateElement(string.Empty, "places", string.Empty);
            doc.AppendChild(placesNode);

            var servers = Enum.GetValues(typeof(PlaceServerEnum));
            foreach (PlaceServerEnum server in servers)
            {
                if (server == PlaceServerEnum.Unknown)
                    continue;

                XmlElement serverNode = doc.CreateElement(string.Empty, server.ToString(), string.Empty);
                placesNode.AppendChild(serverNode);

                var regions = Enum.GetValues(typeof(PlaceRegionEnum));
                foreach (PlaceRegionEnum region in regions)
                {
                    if (region == PlaceRegionEnum.Unknown)
                        continue;

                    XmlElement regionNode = doc.CreateElement(string.Empty, region.ToString(), string.Empty);
                    serverNode.AppendChild(regionNode);

                    foreach (var place in places.Where(place => place.Server == server && place.Region == region))
                    {
                        try
                        {
                            XmlElement placeNode = doc.CreateElement(string.Empty, "place", string.Empty);

                            XmlElement nameNode = doc.CreateElement(string.Empty, "name", string.Empty);
                            XmlText nameText = doc.CreateTextNode(place.Label);
                            nameNode.AppendChild(nameText);
                            placeNode.AppendChild(nameNode);

                            XmlElement typeNode = doc.CreateElement(string.Empty, "type", string.Empty);
                            XmlText typeText = doc.CreateTextNode(place.Icon.ToString());
                            typeNode.AppendChild(typeText);
                            placeNode.AppendChild(typeNode);

                            XmlElement xNode = doc.CreateElement(string.Empty, "x", string.Empty);
                            XmlText xText = doc.CreateTextNode(place.X.ToString());
                            xNode.AppendChild(xText);
                            placeNode.AppendChild(xNode);

                            XmlElement zNode = doc.CreateElement(string.Empty, "z", string.Empty);
                            XmlText zText = doc.CreateTextNode(place.Z.ToString());
                            zNode.AppendChild(zText);
                            placeNode.AppendChild(zNode);

                            regionNode.AppendChild(placeNode);
                        }
                        catch (Exception ex)
                        {
                            Debug.Print($"Cannot save place: {ex.Message}");
                        }
                    }

                }
            }

            try
            {
                doc.Save(fileName);
            }
            catch (Exception ex)
            {
                Debug.Print($"Cannot save {fileName}: {ex.Message}");
            }
        }

        private async void StartServer_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            var httpClient = new HttpClient();
            var ip = await httpClient.GetStringAsync("https://api.ipify.org");
            HostTextBox.Text = ip;

            if (MainWindow.TheLinkServer == null)
            {
                if (String.IsNullOrEmpty(MyNameTextBox.Text) || MyNameTextBox.Text == "(your name)")
                {
                    MessageBox.Show("No name set: please enter a name so others will be able to identify you.", "No name", MessageBoxButton.OK);
                    return;
                }

                if (String.IsNullOrEmpty(PortTextBox.Text))
                {
                    if (MessageBox.Show("No port set: server will be started on port 4443.", "No port", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    {
                        return;
                    }
                    PortTextBox.Text = "4443";
                }
                if (String.IsNullOrEmpty(PasswordTextBox.Text))
                {
                    if (MessageBox.Show("No password set: are you sure you want to start the server with no password?", "No password", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    {
                        return;
                    }
                }

                StartServer.IsEnabled = false;
                LinkToServer.IsEnabled = false;
                BreakConnection.IsEnabled = true;

                try
                {
                    MainWindow.TheLinkServer = new LinkServer(HttpsCheckBox.IsChecked ?? false, int.Parse(PortTextBox.Text), PasswordTextBox.Text);
                    await MainWindow.TheLinkServer.StartServer();
                }
                catch (Exception ex)
                {
                    StartServer.IsEnabled = true;
                    LinkToServer.IsEnabled = false;
                    BreakConnection.IsEnabled = false;
                }
            }
            else
            {
                MessageBoxEx.Show(this, "Server already running?");
            }
        }

        private async void LinkToServer_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (MainWindow.TheLinkClient == null)
            {
                if (String.IsNullOrEmpty(MyNameTextBox.Text) || MyNameTextBox.Text == "(your name)")
                {
                    MessageBox.Show("No name set: please enter a name so others will be able to identify you.", "No name", MessageBoxButton.OK);
                    return;
                }
                if (String.IsNullOrEmpty(HostTextBox.Text))
                {
                    MessageBox.Show("No host set.", "No host", MessageBoxButton.OK);
                    return;
                }
                if (String.IsNullOrEmpty(PortTextBox.Text))
                {
                    if (MessageBox.Show("No port set: client will connect to port 4443.", "No port", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    {
                        return;
                    }
                    PortTextBox.Text = "4443";
                }
                if (String.IsNullOrEmpty(PasswordTextBox.Text))
                {
                    if (MessageBox.Show("No password set: are you sure you want to connect to a server with no password?", "No password", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    {
                        return;
                    }
                }

                StartServer.IsEnabled = false;
                LinkToServer.IsEnabled = false;
                BreakConnection.IsEnabled = true;

                MainWindow TheMainWindow = (MainWindow)Owner;
                try
                {
                    if (!TheMainWindow.MainStatusLabel.Content.ToString().StartsWith("LoUAM Link connecting"))
                    {
                        TheMainWindow.LinkStatusLabel.Foreground = new SolidColorBrush(Colors.Orange);
                        TheMainWindow.LinkStatusLabel.Content = string.Format(
                        "LoUAM Link connecting...");
                    }
                    MainWindow.TheLinkClient = new LinkClient(HttpsCheckBox.IsChecked ?? false, HostTextBox.Text, int.Parse(PortTextBox.Text), PasswordTextBox.Text);
                    await MainWindow.TheLinkClient.ConnectAsync();
                } catch (Exception ex)
                {
                    StartServer.IsEnabled = true;
                    LinkToServer.IsEnabled = false;
                    BreakConnection.IsEnabled = false;
                    if (!TheMainWindow.MainStatusLabel.Content.ToString().StartsWith("LoUAM Link disconnected"))
                    {
                        TheMainWindow.LinkStatusLabel.Foreground = new SolidColorBrush(Colors.Red);
                        TheMainWindow.LinkStatusLabel.Content = string.Format(
                        "LoUAM Link disconnected: {0}",
                        ex.Message);
                    }
                }
            }
            else
            {
                MessageBoxEx.Show(this, "Client already connected?");
            }
        }

        private void BreakConnection_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.TheLinkServer != null)
            {
                MainWindow.TheLinkServer.StopServer();
                MainWindow.TheLinkServer = null;

                StartServer.IsEnabled = true;
                LinkToServer.IsEnabled = true;
                BreakConnection.IsEnabled = false;
            }
            else if (MainWindow.TheLinkClient != null)
            {
                MainWindow.TheLinkClient.Disconnect();
                MainWindow.TheLinkClient = null;

                StartServer.IsEnabled = true;
                LinkToServer.IsEnabled = true;
                BreakConnection.IsEnabled = false;
            }
            else
            {
                MessageBoxEx.Show(this, "No server running, and not connected?");
            }
        }

        private void CopySettings_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AddPlaceButton_Click(object sender, RoutedEventArgs e)
        {
            EditPlace editPlace = new EditPlace();
            editPlace.Owner = this;
            editPlace.ShowDialog();
            RefreshPlaces();
        }

        private void RemovePlaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlacesListView.SelectedItem is Place == false)
            {
                MessageBoxEx.Show(this, "No place selected.", "Remove place", MessageBoxButton.OK);
                return;
            }
            Place SelectedPlace = (Place)PlacesListView.SelectedItem;

            String message = $"Do you really want to remove the place {SelectedPlace.Label}?";
            if (MessageBoxEx.Show(this, message, "Remove place", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Places.Remove(Places.First(place => place.Id == SelectedPlace.Id));
                SavePlaces();
                RefreshPlaces();
            }
        }

        private void EditPlaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlacesListView.SelectedItem is Place == false)
            {
                MessageBoxEx.Show(this, "No place selected.", "Edit place", MessageBoxButton.OK);
                return;
            }
            Place SelectedPlace = (Place)PlacesListView.SelectedItem;

            EditPlace editPlace = new EditPlace(SelectedPlace.Id);
            editPlace.Owner = this;
            editPlace.ShowDialog();
            RefreshPlaces();
        }

        private void MarkerPlaceButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = this.Owner as MainWindow;
            if (mainWindow != null)
            {
                Place SelectedPlace = PlacesListView.SelectedItem as Place;
                mainWindow.AddMarker(SelectedPlace.Id);
            }
        }

        private void LocatePlaceButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxEx.Show(this, "Not implemented!", "Locate Place", MessageBoxButton.OK);
        }

        private void ApplyBrightnessButton_Click(object sender, RoutedEventArgs e)
        {
            Brightness = (float)BrightnessSlider.Value;
            MainWindow TheMainWindow = (MainWindow)Owner;
            TheMainWindow.RefreshMapTiles();
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrightnessLabel != null)
            {
                BrightnessLabel.Content = e.NewValue.ToString("0.00");
            }
        }

        private void TopMostCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            TopMost = TopMostCheckbox.IsChecked ?? false;
            MainWindow mainWindow = this.Owner as MainWindow;
            if (mainWindow != null) mainWindow.RefreshTopMost();
        }

        private void TiltMapCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            TiltMap = TiltMapCheckbox.IsChecked ?? false;
            MainWindow mainWindow = this.Owner as MainWindow;
            if (mainWindow != null) mainWindow.RefreshTiltMap();
        }

        private void TrackPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayersListView.SelectedItem is Player SelectedPlayer)
            {
                if (this.Owner is MainWindow TheMainWindow)
                {
                    // Track selected player
                    ControlPanel.TrackPlayer = true;
                    ControlPanel.TrackPlayerObjectId = SelectedPlayer.ObjectId;
                    ControlPanel.SaveSettings();
                    TheMainWindow.RefreshTrackPlayer();
                }
            }
        }

        private void PlayersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is Player SelectedPlayer)
            {
                if (this.Owner is MainWindow TheMainWindow)
                {
                    // Track selected player
                    ControlPanel.TrackPlayer = true;
                    ControlPanel.TrackPlayerObjectId = SelectedPlayer.ObjectId;
                    ControlPanel.SaveSettings();
                    TheMainWindow.RefreshTrackPlayer();
                }
            }
        }

        private void MarkPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayersListView.SelectedItem is Player SelectedPlayer)
            {
                if (this.Owner is MainWindow TheMainWindow)
                {
                    // Mark selected player
                    TheMainWindow.AddMarker(SelectedPlayer.ObjectId.ToString());
                }
            }
        }
    }
}
