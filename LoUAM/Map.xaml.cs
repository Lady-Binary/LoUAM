using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Path = System.IO.Path;

namespace LoUAM
{
    /// <summary>
    /// Interaction logic for Map.xaml
    /// </summary>
    public partial class Map : UserControl, INotifyPropertyChanged
    {
        public static readonly string MAP_DATA_FOLDER = Path.GetFullPath("./MapData");

        private MarkerServerEnum currentServer = MarkerServerEnum.Unknown;
        public MarkerServerEnum CurrentServer { get => currentServer; set { currentServer = value; this.ServerLabel.Content = $"Server: {value}"; } }

        private MarkerRegionEnum currentRegion = MarkerRegionEnum.Unknown;
        public MarkerRegionEnum CurrentRegion { get { return currentRegion; } set { currentRegion = value; this.RegionLabel.Content = $"Region: {value}"; this.RefreshMapTiles($"{MAP_DATA_FOLDER }/{value}"); } }

        public Map()
        {
            InitializeComponent();

            scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            scrollViewer.MouseLeftButtonUp += OnMouseLeftButtonUp;
            scrollViewer.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
            scrollViewer.PreviewMouseRightButtonUp += OnMouseRightButtonUp;
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;

            scrollViewer.PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
            scrollViewer.MouseMove += OnMouseMove;

            slider.ValueChanged += OnSliderValueChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string prop)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }

        #region Map movement properties and methods
        private Point _lastCenterCoords;
        public Point LastCenterCoords
        {
            get { return _lastCenterCoords; }
            set {
                _lastCenterCoords = value;
                MapCenterWorldCoordsLabel.Content = $"Map center world coords: {value.X:0.00},{value.Y:0.00}";
                OnPropertyChanged("LastCenterCoords");
            }
        }

        private Point _lastMouseMoveCoords;
        public Point LastMouseMoveCoords
        {
            get { return _lastMouseMoveCoords; }
            private set {
                _lastMouseMoveCoords = value;
                MouseWorldCoordsLabel.Content = $"Mouse world coords: {value.X:0.00},{value.Y:0.00}";
                OnPropertyChanged("LastMouseMoveCoords");
            }
        }

        private Point _lastMouseLeftButtonUpCoords;
        public Point LastMouseLeftButtonUpCoords
        {
            get { return _lastMouseLeftButtonUpCoords; }
            private set { _lastMouseLeftButtonUpCoords = value; OnPropertyChanged("LastMouseLeftButtonUpCoords"); }
        }

        private Point _lastMouseRightButtonUpCoords;
        public Point LastMouseRightButtonUpCoords
        {
            get { return _lastMouseRightButtonUpCoords; }
            private set { _lastMouseRightButtonUpCoords = value; OnPropertyChanged("LastMouseRightButtonUpCoords"); }
        }

        private Point? lastCenterPositionOnTarget;
        private Point? lastMousePositionOnTarget;
        private Point? lastDragPoint;

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point posNow = e.GetPosition(scrollViewer);
            LastMouseMoveCoords = scrollViewer.TranslatePoint(posNow, TilesCanvas);

            if (lastDragPoint.HasValue)
            {
                double dX = posNow.X - lastDragPoint.Value.X;
                double dY = posNow.Y - lastDragPoint.Value.Y;

                lastDragPoint = posNow;

                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - dX);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - dY);

                var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2, scrollViewer.ViewportHeight / 2);
                LastCenterCoords = scrollViewer.TranslatePoint(centerOfViewport, TilesCanvas);

                RefreshMapTilesQuality();
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
                slider.Value += slider.TickFrequency;
            }
            if (e.Delta < 0)
            {
                slider.Value -= slider.TickFrequency;
            }

            e.Handled = true;
        }

        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            LastMouseLeftButtonUpCoords = LastMouseMoveCoords;

            scrollViewer.Cursor = Cursors.Arrow;
            scrollViewer.ReleaseMouseCapture();
            lastDragPoint = null;
        }

        void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            LastMouseRightButtonUpCoords = LastMouseMoveCoords;
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

            RefreshMapTilesQuality();
        }

        void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2,
                                             scrollViewer.ViewportHeight / 2);

            if (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0)
            {
                Point? targetBefore = null;
                Point? targetNow = null;

                if (!lastMousePositionOnTarget.HasValue)
                {
                    if (lastCenterPositionOnTarget.HasValue)
                    {
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

                    double multiplicatorX = e.ExtentWidth / MapGrid.ActualWidth;
                    double multiplicatorY = e.ExtentHeight / MapGrid.ActualHeight;

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

            centerOfViewport = new Point(scrollViewer.ViewportWidth / 2, scrollViewer.ViewportHeight / 2);
            LastCenterCoords = scrollViewer.TranslatePoint(centerOfViewport, TilesCanvas);

            RefreshMapTilesQuality();
        }

        private void RefreshMapTilesQuality()
        {
            return;
            int zoom = (int)slider.Value;

            // Set a resolution between 32 and 1024 depending on zoom level
            int dersiredResoltion = Map.GetRequiredResolution((int)slider.Minimum, (int)slider.Maximum, 16, 1024, zoom);

            foreach (MapImage mapImage in TilesCanvas.Children)
            {
                // Check if the current grid item is visible within the scroll viewer

                Point PositionInScrollviewer = mapImage.TranslatePoint(new Point(0, 0), scrollViewer);
                if (
                    PositionInScrollviewer.X >= -(TILE_WIDTH * 2 * scaleTransform.ScaleX) &&
                    PositionInScrollviewer.X <= scrollViewer.ViewportWidth + (TILE_WIDTH * 2 * scaleTransform.ScaleX) &&
                    PositionInScrollviewer.Y >= -(TILE_HEIGHT * 2 * scaleTransform.ScaleY) &&
                    PositionInScrollviewer.Y <= scrollViewer.ViewportHeight + (TILE_HEIGHT * 2 * scaleTransform.ScaleY)
                )
                {
                    //mapImage.UpdateResolution(dersiredResoltion);
                }
            }
        }

        public static int GetRequiredResolution(int SliderMin, int SliderMax, int MinRes, int MaxRes, int SliderValue)
        {
            double scale = (double)(MaxRes - MinRes) / (SliderMax - SliderMin);
            int resolution = (int)(MinRes + ((SliderValue - SliderMin) * scale));
            //return (resolution + 31) / 32 * 32;
            return resolution;
        }

        #endregion

        #region Marker properties and methods
        private Dictionary<MarkerType, Dictionary<string, Marker>> Markers = new Dictionary<MarkerType, Dictionary<string, Marker>>();

        private void AddMarker(Marker marker)
        {
            MapMarker mapMarker = new MapMarker(marker);
            TransformGroup transformGroup = new TransformGroup();
            Binding b = new Binding("scaleTransform");
            //mapMarker.LayoutTransform = transformGroup;
            mapMarker.SetBinding(MapMarker.LayoutTransformProperty, b);
            mapMarker.Name = "Marker_" + marker.Id;
            mapMarker.Tag = marker.Type;
            mapMarker.PreviewMouseWheel += OnPreviewMouseWheel;

            MarkersCanvas.Children.Add(
                mapMarker
                );
            RefreshMarker(marker, mapMarker);
        }

        private void RefreshMarker(Marker marker, FrameworkElement element)
        {
            if (element != null)
            {
                Canvas.SetLeft(element, marker.X);
                Canvas.SetTop(element, marker.Z);

                // scale back so that it preserves aspect ratio
                ScaleTransform scaleTransform = (element as MapMarker).ScaleTransform;
                scaleTransform.ScaleX = 1 / this.scaleTransform.ScaleX;
                scaleTransform.ScaleY = -1 / this.scaleTransform.ScaleY;
            }
        }

        public void Center(double X, double Z)
        {
            Point ScrollLocation = MarkersCanvas.TranslatePoint(new Point(X, Z), MapGrid);

            double offsetX = (ScrollLocation.X * scaleTransform.ScaleX - (scrollViewer.ViewportWidth / 2));
            double offsetY = (ScrollLocation.Y * scaleTransform.ScaleY - (scrollViewer.ViewportHeight / 2));

            scrollViewer.ScrollToHorizontalOffset(offsetX);
            scrollViewer.ScrollToVerticalOffset(offsetY);
        }

        private void RefreshMarkers(Dictionary<MarkerType, Dictionary<string, Marker>> markers)
        {
            foreach (var markerType in markers.Keys)
            {
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

            IEnumerable<Marker> filteredMarkers = markers.Where(m => m.Server == CurrentServer && m.Region == CurrentRegion);

            var markersIds = Markers[markerType].Keys.ToList();

            foreach (Marker marker in filteredMarkers)
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

        #region Tiles properties and methods
        public void RefreshMapTiles()
        {
            RefreshMapTiles($"{Map.MAP_DATA_FOLDER}/{CurrentRegion}");
        }
        private void RefreshMapTiles(string folder)
        {
            TilesCanvas.Children.Clear();

            if (!Directory.Exists(folder))
                return;

            string[] mapTiles = Directory.GetFiles(folder, "*.jpg");
            if (mapTiles == null || mapTiles.Length == 0)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            foreach (string mapTile in mapTiles)
            {
                var SubTile = CreateSubTile(mapTile);
                TilesCanvas.Children.Add(SubTile);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        private Image CreateSubTile(string tilePath)
        {
            MapImage SubTileImage;
            string TileName;
            string TilePrefabPath;

            TileName = Path.GetFileNameWithoutExtension(tilePath);
            TilePrefabPath = tilePath.Replace(".jpg", ".json");
            SubTileImage = new MapImage(tilePath, TilePrefabPath, ControlPanel.Brightness);

            SubTileImage.Name = TileName.Replace(".", "_").Replace(" ", "_").Replace("-", "_");
            SubTileImage.LayoutTransform = TilesCanvas.LayoutTransform.Inverse as Transform;

            return SubTileImage;
        }
        #endregion

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight / 2);
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.ScrollableWidth / 2);
            slider.Value = 1;
        }
    }
}
