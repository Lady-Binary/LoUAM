using System.Drawing.Imaging;
using System.IO;
using AssetStudio;
using Newtonsoft.Json;

namespace LoUAM
{
    internal static class Exporter
    {
        public static bool ExportGameObject(GameObject gameObject)
        {
            return ExportGameObject(gameObject.m_Name, gameObject);
        }
        public static bool ExportGameObject(string name, GameObject gameObject)
        {
            var gameObjectChildrenTransformsDictionary = new System.Collections.Generic.Dictionary<string, object>();
            for(int i=0; i< gameObject.m_Transform.m_Children.Length; i++)
            {
                var childTransform = gameObject.m_Transform.m_Children[i];
                if (childTransform.TryGet(out var transform))
                {
                    ExportTransform(name + $"_{i}", transform);
                }
            }

            return true;
        }

        public static bool ExportTransform(Transform transform)
        {
            return ExportTransform(transform.m_PathID.ToString(), transform);
        }
        public static bool ExportTransform(string name, Transform transform)
        {
            string exportPathName = Path.GetFullPath("./MapData/");

            var localPositionDictionary = new System.Collections.Generic.Dictionary<string, object>
            {
                ["X"] = transform.m_LocalPosition.X,
                ["Y"] = transform.m_LocalPosition.Y,
                ["Z"] = transform.m_LocalPosition.Z
            };
            var localRotationDictionary = new System.Collections.Generic.Dictionary<string, object>
            {
                ["X"] = transform.m_LocalRotation.X,
                ["Y"] = transform.m_LocalRotation.Y,
                ["Z"] = transform.m_LocalRotation.Z,
                ["W"] = transform.m_LocalRotation.W,
            };
            var localScaleDictionary = new System.Collections.Generic.Dictionary<string, object>
            {
                ["X"] = transform.m_LocalScale.X,
                ["Y"] = transform.m_LocalScale.Y,
                ["Z"] = transform.m_LocalScale.Z
            };

            var transformDictionary = new System.Collections.Generic.Dictionary<string, object>
            {
                ["PathID"] = transform.m_PathID,
                ["LocalPosition"] = localPositionDictionary,
                ["LocalRotation"] = localRotationDictionary,
                ["LocalScale"] = localScaleDictionary
            };

            var serializedTransform = JsonConvert.SerializeObject(transformDictionary);

            var exportFullName = Path.Combine(exportPathName, name + ".transform");
            if (ExportFileExists(exportFullName))
                return false;

            File.WriteAllText(exportFullName, serializedTransform);
            return true;
        }

        public static bool ExportTexture2D(Texture2D asset)
        {
            string exportPathName = Path.GetFullPath("./MapData/");
            Texture2DConverter Converter = new Texture2DConverter(asset);

            var bitmap = Converter.ConvertToBitmap(true);
            if (bitmap == null)
                return false;

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
