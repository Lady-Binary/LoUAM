using System;
using System.Collections.Generic;
using System.Linq;
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

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for EditPlace.xaml
    /// </summary>
    public partial class EditPlace : Window
    {
        private string EditingId;

        public EditPlace()
        {
            InitializeComponent();
        }

        public EditPlace(PlaceServerEnum server, PlaceRegionEnum region, double x, double z) : this()
        {
            ServerComboBox.SelectedItem = server;
            RegionComboBox.SelectedItem = region;
            FileComboBox.SelectedItem = PlaceFileEnum.Personal;
            XTextBox.Text = x.ToString("0.00");
            ZTextBox.Text = z.ToString("0.00");
        }

        public EditPlace(string Id) : this()
        {
            this.EditingId = Id;
            Place EditingPlace = ControlPanel.Places.First(Place => Place.Id == Id);
            NameTextBox.Text = EditingPlace.Label;
            TypeComboBox.SelectedItem = EditingPlace.Icon;
            FileComboBox.SelectedItem = EditingPlace.File;
            ServerComboBox.SelectedItem = EditingPlace.Server;
            RegionComboBox.SelectedItem = EditingPlace.Region;
            XTextBox.Text = EditingPlace.X.ToString();
            ZTextBox.Text = EditingPlace.Z.ToString();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            var icons = Enum.GetValues(typeof(PlaceIcon));
            foreach(var icon in icons)
            {
                TypeComboBox.Items.Add(icon);
            }
            var files = Enum.GetValues(typeof(PlaceFileEnum));
            foreach (var file in files)
            {
                FileComboBox.Items.Add(file);
            }
            var servers = Enum.GetValues(typeof(PlaceServerEnum));
            foreach (var server in servers)
            {
                ServerComboBox.Items.Add(server);
            }
            var regions = Enum.GetValues(typeof(PlaceRegionEnum));
            foreach (var region in regions)
            {
                RegionComboBox.Items.Add(region);
            }
        }

        private static readonly Regex _regex = new Regex("[^0-9.,-]+"); //regex that matches disallowed text

        private static bool IsTextAllowed(string text)
        {
            return !_regex.IsMatch(text);
        }

        private void XTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void ZTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string Label = NameTextBox.Text;
            if (string.IsNullOrWhiteSpace(Label))
            {
                ErrorMessageLabel.Content = "Name cannot be empty.";
                ErrorMessageLabel.Visibility = Visibility.Visible;
                NameTextBox.Background = Brushes.Red;
                return;
            }
            else
            {
                NameTextBox.ClearValue(Button.BackgroundProperty);
            }
            if (!Enum.TryParse(TypeComboBox.SelectedItem?.ToString() ?? "", out PlaceIcon Icon))
            {
                ErrorMessageLabel.Content = "No type selected.";
                ErrorMessageLabel.Visibility = Visibility.Visible;
                TypeComboBox.Background = Brushes.Red;
                return;
            }
            else
            {
                TypeComboBox.ClearValue(Button.BackgroundProperty);
            }
            //PlaceIcon Icon = (PlaceIcon)TypeComboBox.SelectedValue;
            if (!double.TryParse(XTextBox.Text, out double X))
            {
                ErrorMessageLabel.Content = "Invalid X coordinate.";
                ErrorMessageLabel.Visibility = Visibility.Visible;
                XTextBox.Background = Brushes.Red;
                return;
            } else
            {
                XTextBox.ClearValue(Button.BackgroundProperty);
            }
            if (!double.TryParse(ZTextBox.Text, out double Z))
            {
                ErrorMessageLabel.Content = "Invalid Y coordinate.";
                ErrorMessageLabel.Visibility = Visibility.Visible;
                ZTextBox.Background = Brushes.Red;
                return;
            }
            else
            {
                ZTextBox.ClearValue(Button.BackgroundProperty);
            }
            if (!Enum.TryParse(ServerComboBox.SelectedItem?.ToString() ?? "", out PlaceServerEnum Server))
            {
                ErrorMessageLabel.Content = "No server selected.";
                ErrorMessageLabel.Visibility = Visibility.Visible;
                ServerComboBox.Background = Brushes.Red;
                return;
            }
            else
            {
                ServerComboBox.ClearValue(Button.BackgroundProperty);
            }
            if (!Enum.TryParse(RegionComboBox.SelectedItem?.ToString() ?? "", out PlaceRegionEnum Region))
            {
                ErrorMessageLabel.Content = "No region selected.";
                ErrorMessageLabel.Visibility = Visibility.Visible;
                RegionComboBox.Background = Brushes.Red;
                return;
            }
            else
            {
                RegionComboBox.ClearValue(Button.BackgroundProperty);
            }
            if (!Enum.TryParse(FileComboBox.SelectedItem?.ToString() ?? "", out PlaceFileEnum File))
            {
                ErrorMessageLabel.Content = "No file selected.";
                ErrorMessageLabel.Visibility = Visibility.Visible;
                FileComboBox.Background = Brushes.Red;
                return;
            }
            else
            {
                FileComboBox.ClearValue(Button.BackgroundProperty);
            }
            if (EditingId == null)
            {
                ControlPanel.Places.Add(new Place(File, Server, Region, PlaceType.Place, Guid.NewGuid().ToString("N"), Icon, Label, X, 0, Z));
            } else
            {
                ControlPanel.Places[ControlPanel.Places.FindIndex(Place => Place.Id == EditingId)] = new Place(File, Server, Region, PlaceType.Place, EditingId, Icon, Label, X, 0, Z);
            }
            ControlPanel.SavePlaces();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
