using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for Map.xaml
    /// </summary>
    public partial class Map : UserControl
    {
        public Map()
        {
            InitializeComponent();

            scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            scrollViewer.MouseLeftButtonUp += OnMouseLeftButtonUp;
            scrollViewer.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;

            scrollViewer.PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
            scrollViewer.MouseMove += OnMouseMove;

            slider.ValueChanged += OnSliderValueChanged;
        }

        #region Map movement methods
        Point? lastCenterPositionOnTarget;
        Point? lastMousePositionOnTarget;
        Point? lastDragPoint;

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (lastDragPoint.HasValue)
            {
                Point posNow = e.GetPosition(scrollViewer);

                double dX = posNow.X - lastDragPoint.Value.X;
                double dY = posNow.Y - lastDragPoint.Value.Y;

                lastDragPoint = posNow;

                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - dX);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - dY);
            }
        }

        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var mousePos = e.GetPosition(scrollViewer);
            if (mousePos.X <= scrollViewer.ViewportWidth && mousePos.Y <
                scrollViewer.ViewportHeight) //make sure we still can use the scrollbars
            {
                scrollViewer.Cursor = Cursors.SizeAll;
                lastDragPoint = mousePos;
                Mouse.Capture(scrollViewer);
            }
        }

        void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            lastMousePositionOnTarget = Mouse.GetPosition(MapGrid);

            if (e.Delta > 0)
            {
                slider.Value += 1;
            }
            if (e.Delta < 0)
            {
                slider.Value -= 1;
            }

            e.Handled = true;
        }

        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollViewer.Cursor = Cursors.Arrow;
            scrollViewer.ReleaseMouseCapture();
            lastDragPoint = null;
        }

        void OnSliderValueChanged(object sender,
             RoutedPropertyChangedEventArgs<double> e)
        {
            // Update map scale
            scaleTransform.ScaleX = e.NewValue;
            scaleTransform.ScaleY = e.NewValue;

            // But resize markers so that they preserve their aspect and position
            RefreshMarkers(this.Markers);

            var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2,
                                             scrollViewer.ViewportHeight / 2);
            lastCenterPositionOnTarget = scrollViewer.TranslatePoint(centerOfViewport, MapGrid);
        }

        void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0)
            {
                Point? targetBefore = null;
                Point? targetNow = null;

                if (!lastMousePositionOnTarget.HasValue)
                {
                    if (lastCenterPositionOnTarget.HasValue)
                    {
                        var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2,
                                                         scrollViewer.ViewportHeight / 2);
                        Point centerOfTargetNow =
                              scrollViewer.TranslatePoint(centerOfViewport, MapGrid);

                        targetBefore = lastCenterPositionOnTarget;
                        targetNow = centerOfTargetNow;
                    }
                }
                else
                {
                    targetBefore = lastMousePositionOnTarget;
                    targetNow = Mouse.GetPosition(MapGrid);

                    lastMousePositionOnTarget = null;
                }

                if (targetBefore.HasValue)
                {
                    double dXInTargetPixels = targetNow.Value.X - targetBefore.Value.X;
                    double dYInTargetPixels = targetNow.Value.Y - targetBefore.Value.Y;

                    double multiplicatorX = e.ExtentWidth / MapGrid.Width;
                    double multiplicatorY = e.ExtentHeight / MapGrid.Height;

                    double newOffsetX = scrollViewer.HorizontalOffset -
                                        dXInTargetPixels * multiplicatorX;
                    double newOffsetY = scrollViewer.VerticalOffset -
                                        dYInTargetPixels * multiplicatorY;

                    if (double.IsNaN(newOffsetX) || double.IsNaN(newOffsetY))
                    {
                        return;
                    }

                    scrollViewer.ScrollToHorizontalOffset(newOffsetX);
                    scrollViewer.ScrollToVerticalOffset(newOffsetY);
                }
            }
        }
        #endregion

        #region Marker methods
        private Dictionary<MarkerType, Dictionary<string, Marker>> Markers = new Dictionary<MarkerType, Dictionary<string, Marker>>();

        private const double DEFAULT_MARKER_WIDTH = 16;
        private const double DEFAULT_MARKER_HEIGHT = 16;

        private (double, double) CalcMarkerSize(FrameworkElement element)
        {
            if (element is Image)
            {
                Image image = element as Image;
                ImageSource imageSource = image.Source;
                return (
                    imageSource.Width * (1 / scaleTransform.ScaleX),
                    imageSource.Height * (1 / scaleTransform.ScaleY)
                    );
            }
            if (element is Ellipse)
            {
                return (
                    DEFAULT_MARKER_WIDTH * (1 / scaleTransform.ScaleX),
                    DEFAULT_MARKER_HEIGHT * (1 / scaleTransform.ScaleY)
                    );
            }
            return (
                DEFAULT_MARKER_WIDTH * (1 / scaleTransform.ScaleX),
                DEFAULT_MARKER_HEIGHT * (1 / scaleTransform.ScaleY)
                );
        }
        private (double, double) CalcMarkerPosition(Marker marker, FrameworkElement element)
        {
            (double x, double y) = WorldXZToMapXY(marker.X, marker.Z);

            if (element is Image)
            {
                Image image = element as Image;
                ImageSource imageSource = image.Source;
                return (
                    x - ((imageSource.Width / 2) / scaleTransform.ScaleX),
                    y - ((imageSource.Height / 2) / scaleTransform.ScaleY)
                    );
            }
            if (element is Ellipse)
            {
                return (
                    x - ((DEFAULT_MARKER_WIDTH / 2) / scaleTransform.ScaleX),
                    y - ((DEFAULT_MARKER_HEIGHT / 2) / scaleTransform.ScaleY)
                    );
            }
            return (
                x - ((DEFAULT_MARKER_WIDTH / 2) / scaleTransform.ScaleX),
                y - ((DEFAULT_MARKER_HEIGHT / 2) / scaleTransform.ScaleY)
                );
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

            var ellipse = new Ellipse();
            ellipse.Fill = Brushes.Transparent;
            ellipse.Triggers.Add(eventTrigger);

            return ellipse;
        }

        private void AddMarker(Marker marker)
        {
            FrameworkElement markerElement;

            switch (marker.Type)
            {
                case MarkerType.CurrentPlayer:
                    {
                        Ellipse ellipse = CreateBlinkingEllipse(Colors.Black, Colors.Cyan);
                        ellipse.Name = "Marker_" + marker.Id;
                        ellipse.Tag = marker.Type;

                        markerElement = ellipse;
                    }
                    break;

                case MarkerType.OtherPlayer:
                    {
                        Ellipse ellipse = CreateBlinkingEllipse(Colors.Black, Colors.LightGreen);
                        ellipse.Name = "Marker_" + marker.Id;
                        ellipse.Tag = marker.Type;

                        markerElement = ellipse;
                    }
                    break;

                default:
                    Image image = new Image
                    {
                        Name = "Marker_" + marker.Id,
                        Source = new BitmapImage(new Uri($"pack://application:,,,/LoUAM;component/Images/{(int)marker.Icon}.png", UriKind.Absolute)),
                        Tag = marker.Type
                    };
                    image.PreviewMouseWheel += OnPreviewMouseWheel;
                    markerElement = image;
                    break;
            }
            MarkersCanvas.Children.Add(
                markerElement
                );
            RefreshMarker(marker, markerElement);
        }
        private void RefreshMarker(Marker marker, FrameworkElement element)
        {
            if (element != null)
            {
                // Refresh its size based on the scale
                (double width, double height) = CalcMarkerSize(element);
                element.Width = width;
                element.Height = height;

                // Refresh its position based on the scale
                (double x, double y) = CalcMarkerPosition(marker, element);
                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
            }
        }

        private double CalcTextBlockFontSize(Marker marker, TextBlock textBlock)
        {
            return 12 / scaleTransform.ScaleX;
        }
        private (double, double) CalcTextBlockPosition(Marker marker, TextBlock textBlock)
        {
            (double x, double y) = WorldXZToMapXY(marker.X, marker.Z);

            return (
                x - (textBlock.ActualWidth / 2),
                y
                );
        }
        private void AddLabel(Marker marker)
        {
            TextBlock NewTextBlock = new TextBlock
            {
                Name = "Label_" + marker.Id,
                Text = marker.Label,
                FontSize = 12,
                Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFFFF")),
                FontWeight = FontWeights.UltraBold,
                Tag = marker.Type
            };
            MarkersCanvas.Children.Add(
                NewTextBlock
                );
            RefreshLabel(marker, NewTextBlock);
        }
        private void RefreshLabel(Marker marker, FrameworkElement element)
        {
            TextBlock textblock = element as TextBlock;
            if (textblock != null)
            {
                // Refresh its size based on the scale
                double fontSize = CalcTextBlockFontSize(marker, textblock);
                textblock.FontSize = fontSize;
                textblock.UpdateLayout();

                // Refresh its position based on the scale
                (double labelx, double labely) = CalcTextBlockPosition(marker, textblock);
                Canvas.SetLeft(textblock, labelx);
                Canvas.SetTop(textblock, labely);

                // Refresh its text if needed
                if (textblock.Text != marker.Label)
                {
                    textblock.Text = marker.Label;
                }
            }
        }

        private (double, double) WorldXZToMapXY(float X, float Z)
        {
            // Bounds taken from: https://legendsofaria.gamepedia.com/Interactive_Map
            //
            // var bounds = [[-3968.87, -3741.21], [3487.13, 3706.79]];
            //
            // Our map is 800x800
            //
            float WORLD_SW_LAT = -3968.87f;
            float WORLD_SW_LNG = -3741.21f;
            float WORLD_NE_LAT = 3487.13f;
            float WORLD_NE_LNG = 3706.79f;

            float MAP_SW_LAT = (float)this.MapViewBox.ActualHeight;
            float MAP_SW_LNG = 0;
            float MAP_NE_LAT = 0;
            float MAP_NE_LNG = (float)this.MapViewBox.ActualWidth;

            float MAP_LAT_TRANSFORM = -1; // real world y asis and image y axis are inverted
            float MAP_LNG_TRANSFORM = 1;

            float MAP_LAT_FACTOR = (WORLD_NE_LAT - WORLD_SW_LAT) / (MAP_NE_LAT - MAP_SW_LAT) * MAP_LAT_TRANSFORM;
            float MAP_LNG_FACTOR = (WORLD_NE_LNG - WORLD_SW_LNG) / (MAP_NE_LNG - MAP_SW_LNG) * MAP_LNG_TRANSFORM;

            float mapLat =
                MAP_LAT_TRANSFORM == 1
                ?
                (Z - WORLD_SW_LAT) / MAP_LAT_FACTOR
                :
                MAP_LAT_TRANSFORM == -1
                ?
                (WORLD_NE_LAT - Z) / MAP_LAT_FACTOR
                :
                0;

            float mapLng =
                MAP_LNG_TRANSFORM == 1
                ?
                (X - WORLD_SW_LNG) / MAP_LNG_FACTOR
                :
                MAP_LNG_TRANSFORM == -1
                ?
                (WORLD_NE_LNG - X) / MAP_LNG_FACTOR
                :
                0;

            return (
                mapLng,
                mapLat
                );
        }

        public void Center(float X, float Z)
        {
            (double mapX, double mapY) = WorldXZToMapXY(X, Z);

            mapX = mapX * scaleTransform.ScaleX;
            mapY = mapY * scaleTransform.ScaleY;

            double offsetX = (mapX - (scrollViewer.ViewportWidth / 2));
            double offsetY = (mapY - (scrollViewer.ViewportHeight / 2));

            scrollViewer.ScrollToHorizontalOffset(offsetX);
            scrollViewer.ScrollToVerticalOffset(offsetY);
        }

        private void RefreshMarkers(Dictionary<MarkerType, Dictionary<string, Marker>> markers)
        {
            foreach (var markerType in markers.Keys) {
                RefreshMarkers(markerType, markers[markerType]);
            }
        }
        private void RefreshMarkers(MarkerType markerType, Dictionary<string, Marker> markers)
        {
            // Get all the elements
            var elements = MarkersCanvas.Children.OfType<FrameworkElement>().Where(i => i.Tag.ToString() == markerType.ToString()).ToDictionary(i => i.Name, i => i);

            foreach (var marker in markers.Values) 
            {
                if (elements.Keys.Contains("Marker_" + marker.Id))
                {
                    // Refresh existing markers
                    RefreshMarker(marker, elements["Marker_" + marker.Id]);
                    elements.Remove("Marker_" + marker.Id);
                }
                else
                {
                    // Add missing markers
                    AddMarker(marker);
                }
                if (marker.Type != MarkerType.CurrentPlayer)
                {
                    if (elements.Keys.Contains("Label_" + marker.Id))
                    {
                        // Refresh existing markers
                        RefreshLabel(marker, elements["Label_" + marker.Id]);
                        elements.Remove("Label_" + marker.Id);
                    }
                    else
                    {
                        // Add missing markers
                        AddLabel(marker);
                    }
                }
            }

            // And remove images that are left of this type, i.e. markers and labels that have no corresponding marker anymore
            foreach (var element in elements)
            {
                MarkersCanvas.Children.Remove(element.Value);
            }
        }

        public void UpdateAllMarkersOfType(MarkerType markerType, IEnumerable<Marker> markers)
        {
            if (!Markers.Keys.Contains(markerType))
            {
                // First marker of this type
                Markers[markerType] = new Dictionary<string, Marker>();
            }

            var markersIds = Markers[markerType].Keys.ToList();

            foreach (Marker marker in markers)
            {
                // Refresh or add existing markers
                Markers[markerType][marker.Id] = marker;
                if (markersIds.Contains(marker.Id)) markersIds.Remove(marker.Id);
            }

            // Remove orphan markers left
            foreach (var markerId in markersIds)
            {
                Markers[markerType].Remove(markerId);
            }

            RefreshMarkers(markerType, Markers[markerType]);
        }
        public void RemoveAllMarkersOfType(MarkerType markerType)
        {
            if (Markers != null && Markers.ContainsKey(markerType))
            {
                Markers[markerType].Clear();
                RefreshMarkers(Markers);
            }
        }

        #endregion
    }
}
