using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Serilog;
using UWPHook.Core.Launch;
using Windows.Management.Deployment;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Finds running processes of a package family: processes with that package identity, and
    /// unpackaged processes (GDK games) whose image lives under the package's install location.
    /// Processes it may not open (system, other users, elevated) are skipped silently.
    /// Make one per game session: it caches the package's install location.
    /// </summary>
    internal sealed class PackageProcessWatcher : IPackageProcessWatcher
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int ErrorSuccess = 0;
        // PACKAGE_FAMILY_NAME_MAX_LENGTH is 64 + 1 + 13; leave room for the terminator.
        private const int FamilyNameCapacity = 128;
        private const int ImagePathCapacity = 32768;

        private readonly Dictionary<string, IReadOnlyList<string>> installLocations = new(StringComparer.OrdinalIgnoreCase);

        public bool AnyAlive(string packageFamilyName)
        {
            var locations = InstallLocationsOf(packageFamilyName);
            var processes = Process.GetProcesses();
            try
            {
                return processes.Any(process => BelongsTo(process.Id, packageFamilyName, locations));
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }

        private static bool BelongsTo(int processId, string packageFamilyName, IReadOnlyList<string> locations)
        {
            using var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (handle.IsInvalid) return false; // access denied, or it has already exited

            return string.Equals(FamilyNameOf(handle), packageFamilyName, StringComparison.OrdinalIgnoreCase)
                || (locations.Count > 0 && IsUnderAny(ImagePathOf(handle), locations));
        }

        private static string? FamilyNameOf(SafeProcessHandle handle)
        {
            var buffer = new char[FamilyNameCapacity];
            var length = (uint)buffer.Length;
            if (GetPackageFamilyName(handle, ref length, buffer) != ErrorSuccess || length == 0) return null;
            return new string(buffer, 0, (int)length - 1); // length counts the terminator
        }

        private static string? ImagePathOf(SafeProcessHandle handle)
        {
            var buffer = new char[ImagePathCapacity];
            var length = (uint)buffer.Length;
            return QueryFullProcessImageName(handle, 0, buffer, ref length) ? new string(buffer, 0, (int)length) : null;
        }

        private static bool IsUnderAny(string? imagePath, IReadOnlyList<string> locations) =>
            imagePath is not null && locations.Any(location => imagePath.StartsWith(location, StringComparison.OrdinalIgnoreCase));

        private IReadOnlyList<string> InstallLocationsOf(string packageFamilyName)
        {
            if (!installLocations.TryGetValue(packageFamilyName, out var locations))
            {
                locations = ReadInstallLocations(packageFamilyName);
                installLocations[packageFamilyName] = locations;
                Log.Debug("Install locations of {FamilyName}: {Locations}", packageFamilyName, locations);
            }
            return locations;
        }

        private static IReadOnlyList<string> ReadInstallLocations(string packageFamilyName)
        {
            try
            {
                return new PackageManager().FindPackagesForUser(string.Empty, packageFamilyName)
                    .Select(InstallLocationOrNull)
                    .OfType<string>()
                    .ToList();
            }
            catch (Exception e)
            {
                Log.Warning("Could not look up the install location of {FamilyName}: {Message}", packageFamilyName, e.Message);
                return [];
            }
        }

        // InstalledLocation, not InstalledPath: InstalledPath needs Windows 10 2004 and the app supports 1809.
        private static string? InstallLocationOrNull(Windows.ApplicationModel.Package package)
        {
            try
            {
                var path = package.InstalledLocation.Path;
                return string.IsNullOrEmpty(path) ? null : Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar;
            }
            catch (Exception e)
            {
                Log.Debug("Package {FullName} has no readable install location: {Message}", package.Id.FullName, e.Message);
                return null;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetPackageFamilyName(SafeProcessHandle process, ref uint packageFamilyNameLength, [Out] char[] packageFamilyName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW", ExactSpelling = true, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] exeName, ref uint size);
    }
}
