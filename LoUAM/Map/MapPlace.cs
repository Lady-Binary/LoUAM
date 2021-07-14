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
        private const double DEFAULT_MARKER_WIDTH = 16;
        private const double DEFAULT_MARKER_HEIGHT = 16;

        public ScaleTransform scaleTransform { get; set; }
        public TranslateTransform translateTransform { get; set; }
        public RotateTransform rotateTransform { get; set; }

        private FrameworkElement placeElement;

        private TextBlock topPlaceLabel;
        public string TopLabel
        {
            get { return topPlaceLabel.Text; }
            set { topPlaceLabel.Text = value; }
        }

        private TextBlock bottomPlaceLabel;
        public string BottomLabel
        {
            get { return bottomPlaceLabel.Text; }
            set { bottomPlaceLabel.Text = value; }
        }

        public double X
        {
            get { return Canvas.GetLeft(this); }
            set { Canvas.SetLeft(this, value); }
        }
        public double Z
        {
            get { return Canvas.GetTop(this); }
            set { Canvas.SetTop(this, value); }
        }

        public MapPlace(Place place)
        {
            TransformGroup transformGroup = new TransformGroup();

            // Center the place exactly on its coordinates,
            // and catch if/when the place resizes so that we re-center
            translateTransform = new TranslateTransform
            {
                X = -this.ActualWidth / 2,
                Y = -this.ActualHeight / 2
            };
            transformGroup.Children.Add(translateTransform);
            this.SizeChanged += MapPlace_SizeChanged;

            // And prepare a scale transform, can be used for example to keep aspect ratio
            scaleTransform = new ScaleTransform
            {
                ScaleX = 1,
                ScaleY = 1
            };
            transformGroup.Children.Add(scaleTransform);

            // Prepare also a rotate transform, can be used when tilt is enabled
            rotateTransform = new RotateTransform();
            if (ControlPanel.TiltMap)
                rotateTransform.Angle = 45;
            else
                rotateTransform.Angle = 0;

            transformGroup.Children.Add(rotateTransform);

            this.RenderTransform = transformGroup;

            // Prepare three rows: one for the top label, one for the icon, and one for the bottom label
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = 20 });
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = DEFAULT_MARKER_HEIGHT });
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = 20 });

            // Top label
            this.topPlaceLabel = new TextBlock
            {
                Name = "TopLabel_" + place.Id,
                Text = "",
                FontSize = 12,
                Foreground = Brushes.Yellow,
                Tag = place.Type
            };
            this.topPlaceLabel.SetValue(Grid.RowProperty, 0);
            this.Children.Add(this.topPlaceLabel);

            // Icon
            switch (place.Type)
            {
                case PlaceType.CurrentPlayer:
                    this.placeElement = CreateBlinkingEllipse(Colors.Black, Colors.Cyan);
                    break;

                case PlaceType.OtherPlayer:
                    this.placeElement = CreateBlinkingEllipse(Colors.Black, Colors.LightGreen);
                    break;

                default:
                    this.placeElement = new Image
                    {
                        Source = new BitmapImage(new Uri($"pack://application:,,,/LoUAM;component/Images/{(int)place.Icon}.png", UriKind.Absolute)),
                    };
                    break;
            }
            placeElement.SetValue(Grid.RowProperty, 1);
            this.Children.Add(this.placeElement);

            // Bottom label
            this.bottomPlaceLabel = new TextBlock
            {
                Name = "BottomLabel_" + place.Id,
                Text = place.Label,
                FontSize = 12,
                Foreground = Brushes.Yellow,
                Tag = place.Type
            };
            this.bottomPlaceLabel.SetValue(Grid.RowProperty, 2);
            this.Children.Add(this.bottomPlaceLabel);
        }

        private void MapPlace_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-center the place exactly on its coordinates
            translateTransform.X = -this.ActualWidth / 2;
            translateTransform.Y = -this.ActualHeight / 2;
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
