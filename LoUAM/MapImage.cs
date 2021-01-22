using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using LoU;

namespace LoUAM
{
    internal class MapImage : System.Windows.Controls.Image
    {
        private ScaleTransform scaleTansform;
        public ScaleTransform ScaleTransform { get => scaleTansform; set => scaleTansform = value; }

        private TranslateTransform centerTransform;
        public TranslateTransform TranslateTransform { get => centerTransform; set => centerTransform = value; }

        public string TileImagePath;
        public string TilePrefabPath;

        public int Resolution = 256;

        public MapImage(string TileImagePath, string TilePrefabPath)
        {
            TransformGroup transformGroup = new TransformGroup();

            // Center the tile exactly on its coordinates,
            // and catch if/when the tile resizes so that we re-center
            TranslateTransform = new TranslateTransform
            {
                X = -this.ActualWidth / 2,
                Y = -this.ActualHeight / 2
            };
            transformGroup.Children.Add(TranslateTransform);
            this.SizeChanged += MapImage_SizeChanged;

            // And prepare a scale transform, can be used for example for keeping the aspect ratio
            ScaleTransform = new ScaleTransform
            {
                ScaleX = 1,
                ScaleY = 1
            };
            transformGroup.Children.Add(ScaleTransform);

            this.RenderTransform = transformGroup;

            // Show the tile image
            this.TileImagePath = TileImagePath;
            this.SetTileImage();

            // And grab the position from the prefab
            this.TilePrefabPath = TilePrefabPath;
            this.SetTilePositionFromPrefab();
        }

        private void MapImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-center the tile exactly on its coordinates
            centerTransform.X = -this.ActualWidth / 2;
            centerTransform.Y = -this.ActualHeight / 2;
        }

        private BitmapImage GetScaledImage(string uriSource, int Resolution)
        {
            Image img = new Image();

            var buffer = File.ReadAllBytes(uriSource);
            MemoryStream ms = new MemoryStream(buffer);
            BitmapImage src = new BitmapImage();
            src.BeginInit();
            src.StreamSource = ms;
            src.DecodePixelHeight = Resolution;
            src.DecodePixelWidth = Resolution;
            src.EndInit();

            return src;
        }

        public void UpdateResolution(int Resolution)
        {
            this.Resolution = Resolution;
            this.SetTileImage();
        }

        private void SetTileImage()
        {
            this.Source = GetScaledImage(this.TileImagePath, this.Resolution);
        }

        private void SetTilePositionFromPrefab()
        {
            if (TileImagePath == null || TileImagePath.Trim() == "")
            {
                throw new System.Exception("Tile has no name, cannot calculate its position.");
            }

            string TileName = Path.GetFileNameWithoutExtension(TileImagePath);
            int TileIndex = int.Parse(TileName.Substring(TileName.Length - 1, 1));
            TileName = TileName.Substring(0, TileName.Length - 2);

            string prefab = File.ReadAllText(this.TilePrefabPath);
            TileTransform tileTransform = JsonConvert.DeserializeObject<TileTransform>(prefab);
            double X = tileTransform.x;
            double Z = tileTransform.z;

            this.SetValue(Canvas.LeftProperty, X);
            this.SetValue(Canvas.TopProperty, Z);
        }
    }
}