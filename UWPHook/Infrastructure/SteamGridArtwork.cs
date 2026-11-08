using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    internal sealed class SteamGridArtwork : IArtworkInstaller
    {
        public SteamGridArtwork(SteamGridDbService sgdb, string tempRoot, string iconsFolder, Action onKeyRejected)
        {
        }

        public Task PrepareAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct) => throw new NotImplementedException();

        public string IconFor(ExportApp app) => throw new NotImplementedException();

        public void InstallFor(string userFolder, IReadOnlyDictionary<string, uint> gridIdsByAumid) => throw new NotImplementedException();

        public void CleanUp() => throw new NotImplementedException();
    }
}
