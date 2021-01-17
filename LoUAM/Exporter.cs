using System.Drawing.Imaging;
using System.IO;
using AssetStudio;

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
            //ImageFormat format = ImageFormat.Png;

            var exportFullName = Path.Combine(exportPathName,  asset.m_Name + ".jpg");
            if (ExportFileExists(exportFullName))
                return false;

            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            Encoder myEncoder = Encoder.Quality;
            EncoderParameters myEncoderParameters = new EncoderParameters(1);
            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 50L);

            myEncoderParameters.Param[0] = myEncoderParameter;

            bitmap.Save(exportFullName, jpgEncoder, myEncoderParameters);
            bitmap.Dispose();
            return true;
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }

            return null;
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
