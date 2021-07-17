using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LoUAM
{
    public partial class MapPlace : System.Windows.Controls.Grid
    {
        private Map ParentMap;

        private const double DEFAULT_MARKER_WIDTH = 16;
        private const double DEFAULT_MARKER_HEIGHT = 16;

        public ScaleTransform scaleTransform { get; set; }
        public TranslateTransform translateTransform { get; set; }
        public RotateTransform rotateTransform { get; set; }

        public FrameworkElement IconElement;
        public TextBlock TopLabel;

        public TextBlock BottomLabel;

        public MapPlace(Map parentMap, Place place)
        {
            ParentMap = parentMap;

            Name = "Place_" + place.Id;
            Tag = place.Type;

            // Store a reference to the parent map

            // Prepare all our transforms
            TransformGroup transformGroup = new TransformGroup();

            // Center the place exactly on its coordinates,
            // and catch if/when the place resizes so that we re-center
            translateTransform = new TranslateTransform
            {
                X = -this.ActualWidth / 2,
                Y = -this.ActualHeight / 2
            };
            transformGroup.Children.Add(translateTransform);

            // And prepare a scale transform, can be used for example to keep aspect ratio
            scaleTransform = new ScaleTransform();
            transformGroup.Children.Add(scaleTransform);

            // Prepare also a rotate transform, can be used when tilt is enabled
            rotateTransform = new RotateTransform();
            transformGroup.Children.Add(rotateTransform);

            this.RenderTransform = transformGroup;

            // Prepare three rows: one for the top label, one for the icon, and one for the bottom label
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = 20 });
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = DEFAULT_MARKER_HEIGHT });
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = 20 });

            // Prepare top label
            this.TopLabel = new TextBlock
            {
                Name = "TopLabel_" + place.Id,
                FontSize = 12,
                Foreground = Brushes.Yellow,
                Tag = place.Type,
            };
            this.TopLabel.SetValue(Grid.RowProperty, 0);
            this.Children.Add(this.TopLabel);

            // Prepare icon
            switch (place.Type)
            {
                case PlaceType.CurrentPlayer:
                    this.IconElement = CreateBlinkingEllipse(Colors.Black, Colors.Cyan);
                    break;

                case PlaceType.OtherPlayer:
                    this.IconElement = CreateBlinkingEllipse(Colors.Black, Colors.LightGreen);
                    break;

                default:
                    this.IconElement = new Image
                    {
                        Source = new BitmapImage(new Uri($"pack://application:,,,/LoUAM;component/Images/{(int)place.Icon}.png", UriKind.Absolute)),
                    };
                    break;
            }
            IconElement.SetValue(Grid.RowProperty, 1);
            this.Children.Add(this.IconElement);

            // Prepare bottom label
            this.BottomLabel = new TextBlock
            {
                Name = "BottomLabel_" + place.Id,
                FontSize = 12,
                Foreground = Brushes.Yellow,
                Tag = place.Type
            };
            this.BottomLabel.SetValue(Grid.RowProperty, 2);
            this.Children.Add(this.BottomLabel);
        }

        public void Refresh(Place place)
        {
            // Center the place exactly on its coordinates,
            // and catch if/when the place resizes so that we re-center
            // Update its position, if necessary
            if (Canvas.GetLeft(this) != place.X)
                Canvas.SetLeft(this, place.X);
            if (Canvas.GetTop(this) != place.Z)
                Canvas.SetTop(this, place.Z);

            // Always scale back with respect of the parent so that it preserves aspect ratio
            this.scaleTransform.ScaleX = 1 / ParentMap.scaleTransform.ScaleX;
            this.scaleTransform.ScaleY = -1 / ParentMap.scaleTransform.ScaleY;
            //this.UpdateLayout();

            // Re-center the place exactly on its coordinates
            translateTransform.X = -this.ActualWidth / 2;
            translateTransform.Y = -this.ActualHeight / 2;

            // Prepare also a rotate transform, can be used when tilt is enabled
            if (ControlPanel.TiltMap)
                rotateTransform.Angle = 45;
            else
                rotateTransform.Angle = 0;

            // Top label
            this.TopLabel.Text = "";
            if (place.Type == PlaceType.Place) this.TopLabel.Visibility = ControlPanel.ShowLabels ? Visibility.Visible : Visibility.Collapsed;

            // Icon
            if (place.Type == PlaceType.Place) this.IconElement.Visibility = ControlPanel.ShowIcons ? Visibility.Visible : Visibility.Collapsed;

            // Bottom label
            this.BottomLabel.Text = place.Label;
            if (place.Type == PlaceType.Place) this.BottomLabel.Visibility = ControlPanel.ShowLabels ? Visibility.Visible : Visibility.Collapsed;
        }

        private Ellipse CreateBlinkingEllipse(Color color1, Color color2)
        {
            ObjectAnimationUsingKeyFrames animation = new ObjectAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromSeconds(0),
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever,
                FillBehavior = FillBehavior.HoldEnd
            };

            DiscreteObjectKeyFrame keyFrame1 = new DiscreteObjectKeyFrame(color1, TimeSpan.FromSeconds(0));
            animation.KeyFrames.Add(keyFrame1);

            DiscreteObjectKeyFrame keyFrame2 = new DiscreteObjectKeyFrame(color2, TimeSpan.FromSeconds(1));
            animation.KeyFrames.Add(keyFrame2);

            Storyboard.SetTargetProperty(animation, new PropertyPath("(Ellipse.Fill).(SolidColorBrush.Color)"));

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            var beginStoryboard = new BeginStoryboard();
            beginStoryboard.Storyboard = storyboard;

            var eventTrigger = new EventTrigger();
            eventTrigger.Actions.Add(beginStoryboard);
            eventTrigger.RoutedEvent = Ellipse.LoadedEvent;

            var ellipse = new Ellipse()
            {
                Fill = new SolidColorBrush(color1),
                Height = DEFAULT_MARKER_WIDTH,
                Width = DEFAULT_MARKER_WIDTH,
                Stroke = new SolidColorBrush(color2)
            };
            ellipse.Triggers.Add(eventTrigger);

            return ellipse;
        }
    }
}
