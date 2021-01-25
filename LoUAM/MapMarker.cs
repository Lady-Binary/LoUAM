using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LoUAM
{
    public partial class MapMarker : System.Windows.Controls.Grid
    {
        private const double DEFAULT_MARKER_WIDTH = 16;
        private const double DEFAULT_MARKER_HEIGHT = 16;

        private ScaleTransform scaleTansform;
        public ScaleTransform ScaleTransform { get => scaleTansform; set => scaleTansform = value; }

        private TranslateTransform centerTransform;
        public TranslateTransform TranslateTransform { get => centerTransform; set => centerTransform = value; }

        private TextBlock topMarkerLabel;
        private FrameworkElement markerElement;
        private TextBlock bottomMarkerLabel;

        public MapMarker(Marker marker)
        {
            TransformGroup transformGroup = new TransformGroup();

            // Center the marker exactly on its coordinates,
            // and catch if/when the marker resizes so that we re-center
            TranslateTransform = new TranslateTransform
            {
                X = -this.ActualWidth / 2,
                Y = -this.ActualHeight / 2
            };
            transformGroup.Children.Add(TranslateTransform);
            this.SizeChanged += MapMarker_SizeChanged;

            // And prepare a scale transform, can be used for example to keep aspect ratio
            ScaleTransform = new ScaleTransform
            {
                ScaleX = 1,
                ScaleY = 1
            };
            transformGroup.Children.Add(ScaleTransform);

            this.RenderTransform = transformGroup;

            // Prepare three rows: one for the top label, one for the icon, and one for the bottom label
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = 20 });
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = DEFAULT_MARKER_HEIGHT });
            this.RowDefinitions.Add(new RowDefinition() { MinHeight = 20 });

            // Top label
            this.topMarkerLabel = new TextBlock
            {
                Name = "TopLabel_" + marker.Id,
                Text = "",
                FontSize = 12,
                Foreground = Brushes.Yellow,
                Tag = marker.Type
            };
            this.topMarkerLabel.SetValue(Grid.RowProperty, 0);
            this.Children.Add(this.topMarkerLabel);

            // Icon
            switch (marker.Type)
            {
                case MarkerType.CurrentPlayer:
                    this.markerElement = CreateBlinkingEllipse(Colors.Black, Colors.Cyan);
                    break;

                case MarkerType.OtherPlayer:
                    this.markerElement = CreateBlinkingEllipse(Colors.Black, Colors.LightGreen);
                    break;

                default:
                    this.markerElement = new Image
                    {
                        Source = new BitmapImage(new Uri($"pack://application:,,,/LoUAM;component/Images/{(int)marker.Icon}.png", UriKind.Absolute)),
                    };
                    break;
            }
            markerElement.SetValue(Grid.RowProperty, 1);
            this.Children.Add(this.markerElement);

            // Bottom label
            this.bottomMarkerLabel = new TextBlock
            {
                Name = "BottomLabel_" + marker.Id,
                Text = marker.Label,
                FontSize = 12,
                Foreground = Brushes.Yellow,
                Tag = marker.Type
            };
            this.bottomMarkerLabel.SetValue(Grid.RowProperty, 2);
            this.Children.Add(this.bottomMarkerLabel);
        }

        private void MapMarker_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-center the marker exactly on its coordinates
            TranslateTransform.X = -this.ActualWidth / 2;
            TranslateTransform.Y = -this.ActualHeight / 2;
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
