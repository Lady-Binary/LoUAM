using System.Drawing.Imaging;
using System.IO;
using AssetStudio;
using System.Reflection;

namespace LoUAM
{
    internal static class Exporter
    {

        public static bool ExportTexture2D(Texture2D asset)
        {
            string exportPathName = Path.GetFullPath("./MapData/");
            Texture2DConverter Converter = new Texture2DConverter(asset);

            //Assembly.Load();

            var bitmap = Converter.ConvertToBitmap(true);
            if (bitmap == null)
                return false;
            ImageFormat format = ImageFormat.Png;

            var exportFullName = Path.Combine(exportPathName,  asset.m_Name + ".png");
            if (ExportFileExists(exportFullName))
                return false;

            bitmap.Save(exportFullName, format);
            bitmap.Dispose();
            return true;

        }

        private static bool ExportFileExists(string filename)
        {
            if (File.Exists(filename))
            {
                return true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(filename));
            return false;
        }

    }
}
