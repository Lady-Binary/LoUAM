using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        private const double MARKER_WIDTH = 25;
        private const double MARKER_HEIGHT = 41;

        private const double MARKER_ANCHOR_X = 13;
        private const double MARKER_ANCHOR_Y = 41;

        private (double, double) CalcMarkerImageSize(Marker marker)
        {
            return (
                MARKER_WIDTH * (1 / scaleTransform.ScaleX),
                MARKER_HEIGHT * (1 / scaleTransform.ScaleY)
                );
        }
        private (double, double) CalcMarkerImagePosition(Marker marker)
        {
            (double x, double y) = WorldXZToMapXY(marker.X, marker.Z);

            return (
                x - (MARKER_ANCHOR_X / scaleTransform.ScaleX),
                y - (MARKER_ANCHOR_Y / scaleTransform.ScaleY)
                );
        }

        private void AddMarkerImage(Marker marker)
        {
            Image NewMarker = new Image
            {
                Name = "Image_" + marker.Id,
                Source = new BitmapImage(new Uri($"images/{marker.Icon.ToString()}_marker.png", UriKind.Relative)),
                Tag = marker.Type
            };
            NewMarker.PreviewMouseWheel += OnPreviewMouseWheel;
            MarkersCanvas.Children.Add(
                NewMarker
                );
            RefreshMarkerImage(marker, NewMarker);
        }
        private void RefreshMarkerImage(Marker marker, Image image)
        {
            if (image != null)
            {
                // Refresh its size based on the scale
                (double width, double height) = CalcMarkerImageSize(marker);
                image.Width = width;
                image.Height = height;

                // Refresh its position based on the scale
                (double x, double y) = CalcMarkerImagePosition(marker);
                Canvas.SetLeft(image, x);
                Canvas.SetTop(image, y);
            }
        }
        private void RemoveMarkerImage(Image image)
        {
            MarkersCanvas.Children.Remove(image);
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
        private void AddMarkerTextBlock(Marker marker)
        {
            TextBlock NewTextBlock = new TextBlock
            {
                Name = "TextBlock_" + marker.Id,
                Text = marker.Label,
                FontSize = 12,
                Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFFFF")),
                FontWeight = FontWeights.UltraBold,
                Tag = marker.Type
            };
            MarkersCanvas.Children.Add(
                NewTextBlock
                );
            RefreshMarkerTextBlock(marker, NewTextBlock);
        }
        private void RefreshMarkerTextBlock(Marker marker, TextBlock textblock)
        {
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

        private void RemoveMarkerTextBlock(TextBlock textblock)
        {
            MarkersCanvas.Children.Remove(textblock);
        }

        private void RefreshMarkers(Dictionary<MarkerType, Dictionary<string, Marker>> markers)
        {
            foreach (var markerType in markers.Keys) {
                RefreshMarkers(markerType, markers[markerType]);
            }
        }
        private void RefreshMarkers(MarkerType markerType, Dictionary<string, Marker> markers)
        {
            // Get all the images of this type
            var images = MarkersCanvas.Children.OfType<Image>().Where(i => i.Tag.ToString() == markerType.ToString()).ToDictionary(i => i.Name, i => i);
            var textblocks = MarkersCanvas.Children.OfType<TextBlock>().Where(i => i.Tag.ToString() == markerType.ToString()).ToDictionary(i => i.Name, i => i);

            foreach (var marker in markers.Values) 
            {
                if (images.Keys.Contains("Image_" + marker.Id))
                {
                    // Refresh existing markers
                    RefreshMarkerImage(marker, images["Image_" + marker.Id]);
                    images.Remove("Image_" + marker.Id);
                }
                else
                {
                    // Add missing markers
                    AddMarkerImage(marker);
                }
                if (textblocks.Keys.Contains("TextBlock_" + marker.Id))
                {
                    // Refresh existing markers
                    RefreshMarkerTextBlock(marker, textblocks["TextBlock_" + marker.Id]);
                    textblocks.Remove("TextBlock_" + marker.Id);
                }
                else
                {
                    // Add missing markers
                    AddMarkerTextBlock(marker);
                }
            }

            // And remove images that are left of this type, i.e. images that have no corresponding marker anymore
            foreach (var image in images)
            {
                RemoveMarkerImage(image.Value);
            }
            foreach (var textblock in textblocks)
            {
                RemoveMarkerTextBlock(textblock.Value);
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
