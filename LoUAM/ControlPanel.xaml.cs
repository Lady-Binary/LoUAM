using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
    public partial class ControlPanel : Window
    {
        public static string MyName = "(your name)";
        public static string Host = "";
        public static int Port = 4443;
        public static string Password = "";

        public static bool TrackPlayer = true;

        public static List<Marker> Places = new List<Marker>();

        public ControlPanel()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MyNameTextBox.Text = MyName;
            HostTextBox.Text = Host;
            PortTextBox.Text = Port.ToString();
            PasswordTextBox.Text = Password;

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
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MyName = MyNameTextBox.Text;
            Host = HostTextBox.Text;
            Port = int.TryParse(PortTextBox.Text, out int i) ? i : 4443;
            Password = PasswordTextBox.Text;
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

            TrackPlayer = bool.TryParse(LoUAMKey.GetValue("TrackPlayer", true).ToString(), out bool b) ? b : true;
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

            LoUAMKey.SetValue("TrackPlayer", TrackPlayer);
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

        public static void LoadPlaces()
        {
            Places = new List<Marker>();

            XmlDocument doc;
            try
            {
                doc = new XmlDocument();
                doc.Load("places.xml");
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show($"Cannot load places.xml: {ex.Message}");
                return;
            }

            XmlNode placesNode = doc.DocumentElement.SelectSingleNode("/places");

            foreach (XmlNode placeNode in doc.DocumentElement.ChildNodes)
            {
                try
                {
                    XmlNode labelNode = placeNode.SelectSingleNode("label");
                    string label = labelNode.InnerText;

                    XmlNode iconNode = placeNode.SelectSingleNode("icon");
                    MarkerIcon icon = Enum.TryParse<MarkerIcon>(iconNode.InnerText, true, out icon) ? icon : MarkerIcon.none;

                    XmlNode latNode = placeNode.SelectSingleNode("lat");
                    float lat = float.TryParse(latNode.InnerText, out lat) ? lat : 0;

                    XmlNode lngNode = placeNode.SelectSingleNode("lng");
                    float lng = float.TryParse(lngNode.InnerText, out lng) ? lng : 0;

                    Marker marker = new Marker(MarkerType.Place, Guid.NewGuid().ToString("N"), icon, label, lng, 0, lat);

                    Places.Add(marker);
                }
                catch (Exception ex)
                {
                    Debug.Print($"Cannot load place: {ex.Message}");
                }

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
                    MainWindow.TheServer = new Server(int.Parse(PortTextBox.Text), PasswordTextBox.Text);
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
                    MainWindow.TheClient = new Client(HostTextBox.Text, int.Parse(PortTextBox.Text), PasswordTextBox.Text);
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
    }
}
