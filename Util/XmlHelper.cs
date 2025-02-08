using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace EmmyLuaSnippetGenerator
{
    public static class XmlHelper
    {
        public static void SaveConfig<T>(T config, string filePath)
        {
            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }

            XmlSerializer serializer = new(typeof(T));

            using StreamWriter writer = new(filePath);

            serializer.Serialize(writer, config);
        }

        public static bool TryLoadConfig<T>(string filePath, out T config)
        {
            if (!File.Exists(filePath))
            {
                config = default;
                return false;
            }

            XmlSerializer serializer = new(typeof(T));
            using StreamReader reader = new(filePath);
        
            config = (T)serializer.Deserialize(reader);
            return true;
        }

        public static void OpenWithDefaultEditor(string filePath)
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
    }
}

