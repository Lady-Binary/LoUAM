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

        public EditPlace(double X, double Z) : this()
        {
            XTextBox.Text = X.ToString("0.00");
            ZTextBox.Text = Z.ToString("0.00");
        }

        public EditPlace(string Id) : this()
        {
            this.EditingId = Id;
            Marker EditingMarker = ControlPanel.Places.First(Place => Place.Id == Id);
            NameTextBox.Text = EditingMarker.Label;
            TypeComboBox.SelectedItem = EditingMarker.Icon;
            XTextBox.Text = EditingMarker.X.ToString();
            ZTextBox.Text = EditingMarker.Z.ToString();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            var values = Enum.GetValues(typeof(MarkerIcon));
            foreach(var value in values)
            {
                TypeComboBox.Items.Add(value);
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
            if (!Enum.TryParse(TypeComboBox.SelectedItem?.ToString() ?? "", out MarkerIcon Icon))
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
            //MarkerIcon Icon = (MarkerIcon)TypeComboBox.SelectedValue;
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
            if (EditingId == null)
            {
                ControlPanel.Places.Add(new Marker(MarkerType.Place, Guid.NewGuid().ToString("N"), Icon, Label, X, 0, Z));
            } else
            {
                ControlPanel.Places[ControlPanel.Places.FindIndex(Place => Place.Id == EditingId)] = new Marker(MarkerType.Place, EditingId, Icon, Label, X, 0, Z);
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
