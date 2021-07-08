using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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

        public static object PlacesLock = new object();
        public static List<Place> Places = new List<Place>();

        // Map
        public static bool AlwaysOnTop = false;
        public static float Brightness = 1;

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
            RefreshPlayersTimer.Tick += RefreshPlayersTimer_Tick;
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
            AlwaysOnTopCheckbox.IsChecked = AlwaysOnTop;
            BrightnessSlider.Value = Brightness;

            RefreshPlaces();
            RefreshPlayers();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MyName = MyNameTextBox.Text;
            Host = HostTextBox.Text;
            Port = int.TryParse(PortTextBox.Text, out int i) ? i : 4443;
            Password = PasswordTextBox.Text;
            Https = HttpsCheckBox.IsChecked ?? true;

            // Map
            AlwaysOnTop = AlwaysOnTopCheckbox.IsChecked ?? false;
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

        private delegate void RefreshPlayersDelegate();
        private void RefreshPlayers()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new RefreshPlayersDelegate(RefreshPlayers));
                return;
            }
            IEnumerable<Player> Players = null;
            if (MainWindow.TheLinkClient != null)
            {
                lock (MainWindow.TheLinkClient.OtherPlayersLock)
                {
                    Players = MainWindow.TheLinkClient.OtherPlayers;
                    lock (MainWindow.TheLinkClient.CurrentPlayerLock)
                    {
                        if (MainWindow.TheLinkClient.CurrentPlayer != null)
                        {
                            Players = Players.Union(Enumerable.Repeat(MainWindow.TheLinkClient.CurrentPlayer, 1));
                        }
                    }
                }
            }
            if (MainWindow.TheLinkServer != null)
            {
                lock (MainWindow.TheLinkServer.OtherPlayersLock)
                {
                    Players = MainWindow.TheLinkServer.OtherPlayers.Values;
                    lock (MainWindow.TheLinkServer.CurrentPlayerLock)
                    {
                        if (MainWindow.TheLinkServer.CurrentPlayer != null)
                        {
                            Players = Players.Union(Enumerable.Repeat(MainWindow.TheLinkServer.CurrentPlayer, 1));
                        }
                    }
                }
            }
            if (Players != null)
            {
                PlayersListView.ItemsSource = Players.Where(player => player != null).OrderBy(player => player.DisplayName);
            } else
            {
                PlayersListView.ItemsSource = null;
            }
        }

        private void RefreshPlayersTimer_Tick(object sender, EventArgs e)
        {
            RefreshPlayers();
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

            TrackPlayer = bool.TryParse(LoUAMKey.GetValue("TrackPlayer", true).ToString(), out bool b) ? b : true;

            // Map
            AlwaysOnTop = bool.TryParse(LoUAMKey.GetValue("AlwaysOnTop", true).ToString(), out bool alwaysOnTop) ? alwaysOnTop : true;
            Brightness = float.TryParse(LoUAMKey.GetValue("Brightness", 1.0f).ToString(), out float brightness) ? brightness : 1;
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

            // Map
            LoUAMKey.SetValue("AlwaysOnTop", AlwaysOnTop);
            LoUAMKey.SetValue("Brightness", Brightness);
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

                XmlNode zNode = placeNode.SelectSingleNode("z");
                double z = double.TryParse(zNode.InnerText, out z) ? z : 0;

                XmlNode xNode = placeNode.SelectSingleNode("x");
                double x = double.TryParse(xNode.InnerText, out x) ? x : 0;

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
                    MainWindow.TheLinkServer.StartServer();
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

        private void AlwaysOnTopCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            AlwaysOnTop = AlwaysOnTopCheckbox.IsChecked ?? false;
            MainWindow mainWindow = this.Owner as MainWindow;
            if (mainWindow != null) mainWindow.RefreshAlwaysOnTop();
        }
    }
}
