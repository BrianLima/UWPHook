using Microsoft.Win32;
using System;
using System.IO;

namespace UWPHook
{
    public static class SteamManager
    {
        /// <summary>
        /// Returns Steam's current installed path
        /// </summary>
        /// <returns></returns>
        public static string GetSteamFolder()
        {
            string registryPath = @"SOFTWARE\Valve\Steam";

            // Check 64-bit registry view
            using (RegistryKey localKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey key64 = localKey64.OpenSubKey(registryPath))
            {
                if (key64 != null)
                {
                    string path64 = key64.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(path64))
                    {
                        return path64;
                    }
                }
            }

            // Check 32-bit registry view
            using (RegistryKey localKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (RegistryKey key32 = localKey32.OpenSubKey(registryPath))
            {
                if (key32 != null)
                {
                    string path32 = key32.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(path32))
                    {
                        return path32;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all the users on userdata
        /// </summary>
        /// <param name="steamInstallPath">Steam's current installed path</param>
        /// <returns>ListString of users path</returns>
        public static string[] GetUsers(string steamInstallPath)
        {
            return Directory.GetDirectories(steamInstallPath + "\\userdata");
        }
    }
}