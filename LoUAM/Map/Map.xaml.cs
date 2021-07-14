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
using System.Windows.Shapes;
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

        public string MarkerPlaceId { get; set; } = "";

        private bool canScroll = true;

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

        public void CanScroll(bool value)
        {
            canScroll = value;
            if (canScroll)
            {
                this.scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                this.scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                this.slider.Visibility = Visibility.Visible;
            }
            else
            {
                this.scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                this.scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                this.slider.Visibility = Visibility.Collapsed;
            }
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
            set
            {
                _lastCenterCoords = value;
                MapCenterWorldCoordsLabel.Content = $"Map center world coords: {value.X:0.00},{value.Y:0.00}";
                OnPropertyChanged("LastCenterCoords");
            }
        }

        private Point _lastMouseMoveCoords;
        public Point LastMouseMoveCoords
        {
            get { return _lastMouseMoveCoords; }
            private set
            {
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

        private Point? lastWorldCoordinatesOnTarget;
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
            if (!canScroll)
                return;

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
            lastWorldCoordinatesOnTarget = Mouse.GetPosition(TilesCanvas);
            lastMousePositionOnTarget = Mouse.GetPosition(scrollViewer);

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

            if (!lastMousePositionOnTarget.HasValue && !lastWorldCoordinatesOnTarget.HasValue)
            {
                // If it was not initiaded by a mouse wheel scroll. then let's store
                // the world coordinate we have right now at the center of the viewport,
                // so that we will try to restore that after zooming again at the center of the viewport
                lastMousePositionOnTarget = new Point(scrollViewer.ViewportWidth / 2,
                                                scrollViewer.ViewportHeight / 2);
                lastWorldCoordinatesOnTarget = scrollViewer.TranslatePoint(lastMousePositionOnTarget.Value, TilesCanvas);
            }
        }

        void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2,
                                             scrollViewer.ViewportHeight / 2);

            if (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0)
            {
                if (lastMousePositionOnTarget.HasValue && lastWorldCoordinatesOnTarget.HasValue)
                {
                    // Let's try to restore the world coordinate we were having at the mouse target
                    // (note, mouse target might be the center if the slider was used)
                    Point TargetScroll = TilesCanvas.TranslatePoint(lastWorldCoordinatesOnTarget.Value, scrollViewer);

                    var horizontalScrollDelta = lastMousePositionOnTarget.Value.X - TargetScroll.X;
                    var currentHorizontalOffset = scrollViewer.HorizontalOffset;
                    scrollViewer.ScrollToHorizontalOffset(currentHorizontalOffset - horizontalScrollDelta);

                    var verticalScrollDelta = lastMousePositionOnTarget.Value.Y - TargetScroll.Y;
                    var currentVerticalOffset = scrollViewer.VerticalOffset;
                    scrollViewer.ScrollToVerticalOffset(currentVerticalOffset - verticalScrollDelta);

                    lastMousePositionOnTarget = null;
                    lastWorldCoordinatesOnTarget = null;
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

        private void RefreshPlace(Place place, MapPlace mapPlace)
        {
            if (place == null || mapPlace == null)
                return;

            // Update its position, if necessary
            if (mapPlace.X != place.X)
                mapPlace.X = place.X;
            if (mapPlace.Z != place.Z)
                mapPlace.Z = place.Z;

            // Update its label, if necessary
            if (mapPlace.BottomLabel != place.Label)
                mapPlace.BottomLabel = place.Label;

            // Always scale back so that it preserves aspect ratio
            ScaleTransform scaleTransform = mapPlace.scaleTransform;
            scaleTransform.ScaleX = 1 / this.scaleTransform.ScaleX;
            scaleTransform.ScaleY = -1 / this.scaleTransform.ScaleY;

            // And we rotate back, if the map was tilted
            RotateTransform rotateTransform = mapPlace.rotateTransform;
            if (ControlPanel.TiltMap)
                rotateTransform.Angle = 45;
            else
                rotateTransform.Angle = 0;

            // If we are updating the current player, we may need to update the marker line
            if (MarkerPlaceId != "" && place.Type == PlaceType.CurrentPlayer)
                UpdateMarker();

            // If we are updating the marked place, we may need to update the marker line
            if (MarkerPlaceId != "" && place.Id == MarkerPlaceId)
                UpdateMarker();
        }

        public void Center(double X, double Z)
        {
            Point TargetScroll = TilesCanvas.TranslatePoint(new Point(X, Z), scrollViewer);
            Point ScrollCenter = new Point(scrollViewer.ViewportWidth / 2, scrollViewer.ViewportHeight / 2);

            var horizontalScrollDelta = ScrollCenter.X - TargetScroll.X;
            var currentHorizontalOffset = scrollViewer.HorizontalOffset;
            scrollViewer.ScrollToHorizontalOffset(currentHorizontalOffset - horizontalScrollDelta);

            var verticalScrollDelta = ScrollCenter.Y - TargetScroll.Y;
            var currentVerticalOffset = scrollViewer.VerticalOffset;
            scrollViewer.ScrollToVerticalOffset(currentVerticalOffset - verticalScrollDelta);
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
            var mapPlaces = PlacesCanvas.Children.OfType<MapPlace>().Where(i => i != null && i.Tag != null && i.Tag.ToString() == placeType.ToString()).ToDictionary(i => i.Name, i => i);

            foreach (var place in places.Values)
            {
                if (mapPlaces.Keys.Contains("Place_" + place.Id))
                {
                    // Refresh existing places
                    RefreshPlace(place, mapPlaces["Place_" + place.Id]);
                    mapPlaces.Remove("Place_" + place.Id);
                }
                else
                {
                    // Add missing places
                    AddPlace(place);
                }
            }

            // And remove images that are left of this type, i.e. places and labels that have no corresponding place anymore
            foreach (var mapPlace in mapPlaces)
            {
                PlacesCanvas.Children.Remove(mapPlace.Value);
            }
        }

        public void UpdateAllPlacesOfType(PlaceType placeType, IEnumerable<Place> places)
        {
            if (!Places.Keys.Contains(placeType))
            {
                // First place of this type
                Places[placeType] = new Dictionary<string, Place>();
            }

            IEnumerable<Place> filteredPlaces = places.Where(m => m != null && m.Server == CurrentServer && m.Region == CurrentRegion);

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

        public void AddMarker(double x, double y, double z)
        {
            if (this.MarkerPlaceId != "") RemoveMarker();

            if (!Places.Keys.Contains(PlaceType.Marker))
                Places[PlaceType.Marker] = new Dictionary<string, Place>();

            Place marker = new Place(PlaceFileEnum.None, currentServer, currentRegion, PlaceType.Marker, "_Marker", PlaceIcon.none, "", x, y, z);
            this.Places[PlaceType.Marker].Add(marker.Id, marker);
            RefreshPlaces(PlaceType.Marker, this.Places[PlaceType.Marker]);

            AddMarker(marker.Id);
        }
        public void AddMarker(string MarkerPlaceId)
        {
            if (this.MarkerPlaceId != "") RemoveMarker();

            this.MarkerPlaceId = MarkerPlaceId;

            UpdateMarker();
        }
        public void UpdateMarker()
        {
            Place currentPlayerPlace = Places.ContainsKey(PlaceType.CurrentPlayer) ? Places[PlaceType.CurrentPlayer]?.Values?.FirstOrDefault() : null;
            Place markerPlace = Places.Values?.SelectMany(places => places.Values)?.Where(place => place.Id == MarkerPlaceId)?.FirstOrDefault();
            Line markerLine = PlacesCanvas.Children?.OfType<Line>()?.FirstOrDefault();

            if (currentPlayerPlace == null || markerPlace == null)
                return;

            if (currentPlayerPlace.Server != currentServer ||
                currentPlayerPlace.Region != currentRegion ||
                markerPlace.Server != currentServer ||
                markerPlace.Region != currentRegion
                )
            {
                if (markerLine != null) PlacesCanvas.Children.Remove(markerLine);
                return;
            }

            if (markerLine == null)
            {
                markerLine = new Line();
                PlacesCanvas.Children.Add(markerLine);
            }

            markerLine.Stroke = System.Windows.Media.Brushes.White;
            markerLine.StrokeThickness = 4;
            markerLine.X1 = currentPlayerPlace.X;
            markerLine.Y1 = currentPlayerPlace.Z;
            markerLine.X2 = markerPlace.X;
            markerLine.Y2 = markerPlace.Z;
        }
        public void RemoveMarker()
        {
            Line markerLine = PlacesCanvas.Children?.OfType<Line>()?.FirstOrDefault();
            if (markerLine != null)
                PlacesCanvas.Children.Remove(markerLine);

            if (Places.ContainsKey(PlaceType.Marker) && Places[PlaceType.Marker].ContainsKey("_Marker"))
            {
                Places[PlaceType.Marker].Remove("_Marker");
                RefreshPlaces(PlaceType.Marker, this.Places[PlaceType.Marker]);
            }

            this.MarkerPlaceId = "";
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
