namespace UWPHook.Core.Steam;

/// <summary>What adding shortcuts to one Steam account's collections did.</summary>
public enum CollectionsOutcome
{
    /// <summary>Nothing to add: no collection names, no shortcuts, or they were all there already. Nothing was written.</summary>
    Unchanged,

    /// <summary>The shortcuts were added, after a backup of the file.</summary>
    Updated,

    /// <summary>The account's collections could not be found, read or written. Nothing was changed; the log says why.</summary>
    Skipped,
}

/// <summary>The Steam library collections of an account, which is where the Steam client keeps "tags" today.</summary>
public interface ISteamCollections
{
    /// <summary>
    /// Adds the shortcuts <paramref name="appIds"/> (unsigned, as Steam's library knows them) to the collections
    /// named <paramref name="collectionNames"/> of the account in <paramref name="userFolder"/>, creating the
    /// collections that don't exist. Never removes or renames anything, and never throws.
    /// </summary>
    CollectionsOutcome AddShortcuts(string userFolder, IReadOnlyList<string> collectionNames, IReadOnlyCollection<uint> appIds);
}
