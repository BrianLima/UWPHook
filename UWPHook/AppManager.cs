using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UWPHook
{
    /// <summary>
    /// Friendly names for well-known apps (Resources\KnownApps.json).
    /// Launching lives in <see cref="GameLauncher"/>.
    /// </summary>
    static class AppManager
    {
        /// <summary>
        /// Try to convert an aumid into a human-readable app name
        /// </summary>
        /// <param name="appName">Application user model ID (aumid)</param>
        /// <param name="readableName">User-friendly app name</param>
        /// <returns>Whether this is a known app</returns>
        public static bool IsKnownApp(string appName, out string readableName)
        {
            string appsJson = GetEmbeddedResource("KnownApps.json");
            var apps = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(appsJson);

            foreach (var kvp in apps)
            {
                if (appName.StartsWith(kvp.Key + "_"))
                {
                    readableName = kvp.Value;
                    return true;
                }
            }

            readableName = null;
            return false;
        }

        static string GetEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            resourceName = assembly.GetManifestResourceNames().First(r => r.Contains(resourceName));
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
