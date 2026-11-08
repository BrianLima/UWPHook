using Microsoft.Win32;
using Serilog;
using System;
using System.IO;
using System.Linq;
using UWPHook.Core.Steam;

namespace UWPHook
{
    /// <summary>
    /// The one place that finds the Steam install: export, "Remove UWPHook shortcuts" and the Steam restart
    /// all use it, so they always work on the same Steam.
    /// </summary>
    public static class SteamManager
    {
        private const string SteamKey = @"Software\Valve\Steam";

        /// <summary>
        /// Steam's install folder: HKCU SteamPath (kept current by the Steam client), else the installer's
        /// HKLM InstallPath; null when neither folder exists. See <see cref="SteamInstallChooser"/>.
        /// </summary>
        public static string? GetSteamFolder() =>
            SteamInstallChooser.Choose(
                ReadString(Registry.CurrentUser, SteamKey, "SteamPath"),
                ReadHklmInstallPath(RegistryView.Registry32) ?? ReadHklmInstallPath(RegistryView.Registry64),
                Directory.Exists);

        /// <summary>steam.exe in <see cref="GetSteamFolder"/>, or null when it isn't there.</summary>
        public static string? GetSteamExecutable()
        {
            string? folder = GetSteamFolder();
            if (folder is null) return null;

            string exe = Path.Combine(folder, "steam.exe");
            return File.Exists(exe) ? exe : null;
        }

        /// <summary>
        /// The account folders in Steam's userdata (numeric, non-zero ids only), or none when Steam was never
        /// signed in to and has no userdata folder.
        /// </summary>
        /// <param name="steamInstallPath">Steam's install folder</param>
        public static string[] GetUsers(string steamInstallPath)
        {
            string userdata = Path.Combine(steamInstallPath, "userdata");
            if (!Directory.Exists(userdata)) return [];

            return Directory.GetDirectories(userdata)
                .Where(user => SteamUserData.IsAccountFolder(Path.GetFileName(user)))
                .ToArray();
        }

        /// <summary>HKLM\SOFTWARE\Valve\Steam\InstallPath; the 32-bit view is WOW6432Node, where the installer writes it.</summary>
        private static string? ReadHklmInstallPath(RegistryView view)
        {
            try
            {
                using RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                return ReadString(hklm, SteamKey, "InstallPath");
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Could not read Steam's InstallPath from the {View} registry", view);
                return null;
            }
        }

        private static string? ReadString(RegistryKey root, string subKey, string name)
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(subKey);
                return key?.GetValue(name) as string;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Could not read {Name} from {Key}", name, root.Name + "\\" + subKey);
                return null;
            }
        }
    }
}
