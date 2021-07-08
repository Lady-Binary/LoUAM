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

        private PlaceServerEnum currentServer = PlaceServerEnum.Unknown;
        public PlaceServerEnum CurrentServer { get => currentServer; set { currentServer = value; this.ServerLabel.Content = $"Server: {value}"; } }

        private PlaceRegionEnum currentRegion = PlaceRegionEnum.Unknown;
        public PlaceRegionEnum CurrentRegion { get { return currentRegion; } set { currentRegion = value; this.RegionLabel.Content = $"Region: {value}"; this.RefreshMapTiles($"{MAP_DATA_FOLDER }/{value}"); } }

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

            // But resize places so that they preserve their aspect and position
            RefreshPlaces(this.Places);

            var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2,
                                                scrollViewer.ViewportHeight / 2);
            lastCenterPositionOnTarget = scrollViewer.TranslatePoint(centerOfViewport, MapGrid);
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
        }

        public static int GetRequiredResolution(int SliderMin, int SliderMax, int MinRes, int MaxRes, int SliderValue)
        {
            double scale = (double)(MaxRes - MinRes) / (SliderMax - SliderMin);
            int resolution = (int)(MinRes + ((SliderValue - SliderMin) * scale));
            //return (resolution + 31) / 32 * 32;
            return resolution;
        }

        #endregion

        #region Place properties and methods
        private Dictionary<PlaceType, Dictionary<string, Place>> Places = new Dictionary<PlaceType, Dictionary<string, Place>>();

        private void AddPlace(Place place)
        {
            MapPlace mapPlace = new MapPlace(place);
            TransformGroup transformGroup = new TransformGroup();
            Binding b = new Binding("scaleTransform");
            //mapPlace.LayoutTransform = transformGroup;
            mapPlace.SetBinding(MapPlace.LayoutTransformProperty, b);
            mapPlace.Name = "Place_" + place.Id;
            mapPlace.Tag = place.Type;
            mapPlace.PreviewMouseWheel += OnPreviewMouseWheel;

            PlacesCanvas.Children.Add(
                mapPlace
                );
            RefreshPlace(place, mapPlace);
        }

        private void RefreshPlace(Place place, FrameworkElement element)
        {
            if (element != null)
            {
                Canvas.SetLeft(element, place.X);
                Canvas.SetTop(element, place.Z);

                // scale back so that it preserves aspect ratio
                ScaleTransform scaleTransform = (element as MapPlace).ScaleTransform;
                scaleTransform.ScaleX = 1 / this.scaleTransform.ScaleX;
                scaleTransform.ScaleY = -1 / this.scaleTransform.ScaleY;
            }
        }

        public void Center(double X, double Z)
        {
            Point ScrollLocation = PlacesCanvas.TranslatePoint(new Point(X, Z), MapGrid);

            double offsetX = (ScrollLocation.X * scaleTransform.ScaleX - (scrollViewer.ViewportWidth / 2));
            double offsetY = (ScrollLocation.Y * scaleTransform.ScaleY - (scrollViewer.ViewportHeight / 2));

            scrollViewer.ScrollToHorizontalOffset(offsetX);
            scrollViewer.ScrollToVerticalOffset(offsetY);
        }

        private void RefreshPlaces(Dictionary<PlaceType, Dictionary<string, Place>> places)
        {
            foreach (var placeType in places.Keys)
            {
                RefreshPlaces(placeType, places[placeType]);
            }
        }
        private void RefreshPlaces(PlaceType placeType, Dictionary<string, Place> places)
        {
            // Get all the elements
            var elements = PlacesCanvas.Children.OfType<FrameworkElement>().Where(i => i.Tag.ToString() == placeType.ToString()).ToDictionary(i => i.Name, i => i);

            foreach (var place in places.Values)
            {
                if (elements.Keys.Contains("Place_" + place.Id))
                {
                    // Refresh existing places
                    RefreshPlace(place, elements["Place_" + place.Id]);
                    elements.Remove("Place_" + place.Id);
                }
                else
                {
                    // Add missing places
                    AddPlace(place);
                }
            }

            // And remove images that are left of this type, i.e. places and labels that have no corresponding place anymore
            foreach (var element in elements)
            {
                PlacesCanvas.Children.Remove(element.Value);
            }
        }

        public void UpdateAllPlacesOfType(PlaceType placeType, IEnumerable<Place> places)
        {
            if (!Places.Keys.Contains(placeType))
            {
                // First place of this type
                Places[placeType] = new Dictionary<string, Place>();
            }

            IEnumerable<Place> filteredPlaces = places.Where(m => m.Server == CurrentServer && m.Region == CurrentRegion);

            var placesIds = Places[placeType].Keys.ToList();

            foreach (Place place in filteredPlaces)
            {
                // Refresh or add existing places
                Places[placeType][place.Id] = place;
                if (placesIds.Contains(place.Id)) placesIds.Remove(place.Id);
            }

            // Remove orphan places left
            foreach (var placeId in placesIds)
            {
                Places[placeType].Remove(placeId);
            }

            RefreshPlaces(placeType, Places[placeType]);
        }
        public void RemoveAllPlacesOfType(PlaceType placeType)
        {
            if (Places != null && Places.ContainsKey(placeType))
            {
                Places[placeType].Clear();
                RefreshPlaces(Places);
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
