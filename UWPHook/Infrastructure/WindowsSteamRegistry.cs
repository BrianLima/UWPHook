using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    internal sealed class WindowsSteamRegistry : ISteamRegistry
    {
        public WindowsSteamRegistry(string userKey = @"Software\Valve\Steam", string machineKey = @"SOFTWARE\Valve\Steam")
        {
        }

        public string? SteamPath() => throw new NotImplementedException();

        public IReadOnlyList<string?> InstallPaths() => throw new NotImplementedException();
    }
}
