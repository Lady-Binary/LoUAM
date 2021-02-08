using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using LoU;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

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

        public float Brightness = 1;
        public int Resolution = 256;

        public MapImage(string TileImagePath, string TilePrefabPath, float Brightness)
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

            this.Brightness = Brightness;

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

        private BitmapImage GetScaledImage(string uriSource, int Resolution, float Brightness)
        {
            var buffer = File.ReadAllBytes(uriSource);
            MemoryStream ms = new MemoryStream(buffer);
            Bitmap originalBitmap = new Bitmap(ms);
            Bitmap resizedBitmap = new Bitmap(originalBitmap, new System.Drawing.Size(Resolution, Resolution));
            Bitmap resizedAndAdjustedBitmap = new Bitmap(Resolution, Resolution);

            float[][] ptsArray ={
                    new float[] {Brightness, 0, 0, 0, 0},
                    new float[] {0, Brightness, 0, 0, 0},
                    new float[] {0, 0, Brightness, 0, 0},
                    new float[] {0, 0, 0, Brightness, 0},
                    new float[] {0, 0, 0, 0, Brightness},
            };
            ImageAttributes imageAttributes = new ImageAttributes();
            imageAttributes.SetColorMatrix(new ColorMatrix(ptsArray), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            using (Graphics g = Graphics.FromImage(resizedAndAdjustedBitmap))
            {
                g.DrawImage(
                    resizedBitmap,
                    new Rectangle(0, 0, resizedAndAdjustedBitmap.Width, resizedAndAdjustedBitmap.Height),
                    0,
                    0,
                    resizedBitmap.Width,
                    resizedBitmap.Height,
                    GraphicsUnit.Pixel,
                    imageAttributes);
            }
            
            using (var memory = new MemoryStream())
            {
                resizedAndAdjustedBitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var src = new BitmapImage();
                src.BeginInit();
                src.StreamSource = memory;
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.EndInit();
                src.Freeze();

                return src;
            }
        }

        public void UpdateResolution(int Resolution)
        {
            this.Resolution = Resolution;
            this.SetTileImage();
        }

        private void SetTileImage()
        {
            this.Source = GetScaledImage(this.TileImagePath, this.Resolution, this.Brightness);
        }

        private void SetTilePositionFromPrefab()
        {
            if (TileImagePath == null || TileImagePath.Trim() == "")
            {
                throw new System.Exception("Tile has no name, cannot calculate its position.");
            }

            string TileName = Path.GetFileNameWithoutExtension(TileImagePath);
            string TileIndexString = Regex.Match(TileName, @"\d+").Value;
            int TileIndex = int.Parse(TileIndexString);
            TileName = TileName.Substring(0, TileName.Length - 2);

            double X;
            double Z;
            if (File.Exists(this.TilePrefabPath))
            {
                string prefab = File.ReadAllText(this.TilePrefabPath);
                TileTransform tileTransform = JsonConvert.DeserializeObject<TileTransform>(prefab);
                X = tileTransform.x;
                Z = tileTransform.z;
            } else
            {
                switch (TileIndex)
                {
                    case 0:
                        X = 128;
                        Z = 128;
                        break;
                    case 1:
                        X = 128;
                        Z = 128-256;
                        break;
                    case 2:
                        X = 128-256;
                        Z = 128;
                        break;
                    case 3:
                        X = 128-256;
                        Z = 128-256;
                        break;
                    default:
                        X = 0;
                        Z = 0;
                        break;
                }
            }

            this.SetValue(Canvas.LeftProperty, X);
            this.SetValue(Canvas.TopProperty, Z);
        }
    }
}