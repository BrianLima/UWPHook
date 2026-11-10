namespace UWPHook.Core.Steam;

/// <summary>Steam's collections file. Not implemented yet.</summary>
public sealed class CollectionsFile
{
    private readonly byte[] _bytes;

    private CollectionsFile(byte[] bytes) => _bytes = bytes;

    public static CollectionsFile Parse(byte[] bytes) => new(bytes);

    public bool HasCollections => false;

    public static string ManagedIdFor(string name) => name;

    public bool Add(IReadOnlyList<string> names, IReadOnlyCollection<uint> appIds, DateTimeOffset now) => false;

    public byte[] ToBytes() => _bytes;
}
