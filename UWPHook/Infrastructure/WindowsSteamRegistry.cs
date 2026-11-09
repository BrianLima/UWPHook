using Microsoft.Win32;
using Serilog;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Reads Steam's registry values. The keys are parameters only so tests can point them at a throwaway key.
    /// Never throws: a value that can't be read is logged and missing.
    /// </summary>
    internal sealed class WindowsSteamRegistry : ISteamRegistry
    {
        private readonly string _userKey;
        private readonly string _machineKey;

        public WindowsSteamRegistry(string userKey = @"Software\Valve\Steam", string machineKey = @"SOFTWARE\Valve\Steam")
        {
            _userKey = userKey;
            _machineKey = machineKey;
        }

        public string? SteamPath() => ReadString(Registry.CurrentUser, _userKey, "SteamPath");

        /// <summary>The 32-bit view is WOW6432Node, where the installer writes it; the 64-bit view comes second.</summary>
        public IReadOnlyList<string?> InstallPaths() =>
            [ReadMachineInstallPath(RegistryView.Registry32), ReadMachineInstallPath(RegistryView.Registry64)];

        private string? ReadMachineInstallPath(RegistryView view)
        {
            try
            {
                using RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                return ReadString(machine, _machineKey, "InstallPath");
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
