using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Renumber.Core
{
    /// <summary>The last rule used, per Windows user, in %AppData%\Vixeldorf\Renumber.</summary>
    internal static class SettingsStore
    {
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() },
        };

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Vixeldorf", "Renumber", "settings.json");

        public static NumberingRule Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonConvert.DeserializeObject<NumberingRule>(File.ReadAllText(FilePath), Json)
                           ?? new NumberingRule();
            }
            catch (Exception)
            {
                // A damaged settings file must not stop the command; defaults will do.
            }
            return new NumberingRule();
        }

        public static void Save(NumberingRule rule)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(rule, Json));
            }
            catch (Exception)
            {
                // Losing the remembered settings is not worth an error dialog.
            }
        }
    }
}
