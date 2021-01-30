using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
            Map,
            LinkControl
        }

        public static string MyName = "(your name)";
        public static string Host = "";
        public static bool Https = true;
        public static int Port = 4443;
        public static string Password = "";

        public static bool TrackPlayer = true;

        public static List<Marker> Places = new List<Marker>();

        public static float Brightness = 1;

        public ControlPanel()
        {
            InitializeComponent();
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

            if (MainWindow.TheServer != null || MainWindow.TheClient != null)
            {
                StartServer.IsEnabled = false;
                LinkToServer.IsEnabled = false;
                BreakConnection.IsEnabled = true;
            } else
            {
                StartServer.IsEnabled = true;
                LinkToServer.IsEnabled = true;
                BreakConnection.IsEnabled = false;
            }

            BrightnessSlider.Value = Brightness;

            RefreshPlaces();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Brightness = (float)BrightnessSlider.Value;
            MyName = MyNameTextBox.Text;
            Host = HostTextBox.Text;
            Port = int.TryParse(PortTextBox.Text, out int i) ? i : 4443;
            Password = PasswordTextBox.Text;
            Https = HttpsCheckBox.IsChecked ?? true;
            SaveSettings();
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

            Brightness = float.TryParse(LoUAMKey.GetValue("Brightness", 1.0f).ToString(), out float f) ? f : 1;
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
            placesListView.ItemsSource = Places.OrderBy(place => place.Label);
        }

        #endregion
        public static void LoadPlaces()
        {
            List<Marker> CommonPlaces = LoadPlaces("common-places.xml");
            List<Marker> PersonalPlaces = LoadPlaces("personal-places.xml");

            Places = CommonPlaces.Select(p => { p.File = MarkerFile.Common; return p; })
                    .Union(PersonalPlaces.Select(p => { p.File = MarkerFile.Personal; return p; }))
                    .ToList();
        }
        public static List<Marker> LoadPlaces(string fileName)
        {
            List<Marker> LoadedPlaces = new List<Marker>();

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

            XmlNode placesNode = doc.DocumentElement.SelectSingleNode("/places");

            foreach (XmlNode placeNode in doc.DocumentElement.ChildNodes)
            {
                try
                {
                    XmlNode nameNode = placeNode.SelectSingleNode("name");
                    string name = nameNode.InnerText;

                    XmlNode typeNode = placeNode.SelectSingleNode("type");
                    MarkerIcon type = Enum.TryParse<MarkerIcon>(typeNode.InnerText, true, out type) ? type : MarkerIcon.none;

                    XmlNode zNode = placeNode.SelectSingleNode("z");
                    double z = double.TryParse(zNode.InnerText, out z) ? z : 0;

                    XmlNode xNode = placeNode.SelectSingleNode("x");
                    double x = double.TryParse(xNode.InnerText, out x) ? x : 0;

                    Marker marker = new Marker(MarkerFile.None, MarkerType.Place, Guid.NewGuid().ToString("N"), type, name, x, 0, z);

                    LoadedPlaces.Add(marker);
                }
                catch (Exception ex)
                {
                    Debug.Print($"Cannot load place: {ex.Message}");
                }
            }

            return LoadedPlaces;
        }

        public static void SavePlaces()
        {
            List<Marker> CommonPlaces = Places.Where(place => place.File == MarkerFile.Common).ToList();
            SavePlaces("common-places.xml", CommonPlaces);

            List<Marker> PersonalPlaces = Places.Where(place => place.File == MarkerFile.Personal).ToList();
            SavePlaces("personal-places.xml", PersonalPlaces);
        }
        public static void SavePlaces(string fileName, List<Marker> places)
        {
            XmlDocument doc;
            doc = new XmlDocument();

            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            XmlElement placesNode = doc.CreateElement(string.Empty, "places", string.Empty);
            doc.AppendChild(placesNode);

            foreach(var place in places)
            {
                try
                {
                    XmlElement placeNode = doc.CreateElement(string.Empty, "place", string.Empty);
                    placesNode.AppendChild(placeNode);

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

                    placesNode.AppendChild(placeNode);
                }
                catch (Exception ex)
                {
                    Debug.Print($"Cannot save place: {ex.Message}");
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

            if (MainWindow.TheServer == null)
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
                    MainWindow.TheServer = new Server(HttpsCheckBox.IsChecked ?? false, int.Parse(PortTextBox.Text), PasswordTextBox.Text);
                    MainWindow.TheServer.StartServer();
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

            if (MainWindow.TheClient == null)
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
                    MainWindow.TheClient = new Client(HttpsCheckBox.IsChecked ?? false, HostTextBox.Text, int.Parse(PortTextBox.Text), PasswordTextBox.Text);
                    await MainWindow.TheClient.ConnectAsync();
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
            if (MainWindow.TheServer != null)
            {
                MainWindow.TheServer.StopServer();
                MainWindow.TheServer = null;

                StartServer.IsEnabled = true;
                LinkToServer.IsEnabled = true;
                BreakConnection.IsEnabled = false;
            }
            else if (MainWindow.TheClient != null)
            {
                MainWindow.TheClient.Disconnect();
                MainWindow.TheClient = null;

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
            if (placesListView.SelectedItem is Marker == false)
            {
                MessageBoxEx.Show(this, "No place selected.", "Remove place", MessageBoxButton.OK);
                return;
            }
            Marker SelectedPlace = (Marker)placesListView.SelectedItem;

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
            if (placesListView.SelectedItem is Marker == false)
            {
                MessageBoxEx.Show(this, "No place selected.", "Edit place", MessageBoxButton.OK);
                return;
            }
            Marker SelectedPlace = (Marker)placesListView.SelectedItem;

            EditPlace editPlace = new EditPlace(SelectedPlace.Id);
            editPlace.Owner = this;
            editPlace.ShowDialog();
            RefreshPlaces();
        }

        private void MarkerPlaceButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxEx.Show(this, "Not implemented!", "Marker Place", MessageBoxButton.OK);
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
    }
}
