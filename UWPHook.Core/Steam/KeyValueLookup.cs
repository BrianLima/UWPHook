using System.Diagnostics.CodeAnalysis;
using ValveKeyValue;

namespace UWPHook.Core.Steam;

/// <summary>Finds a child of a KeyValues object by key, ignoring case: Steam's files don't always spell keys the same way.</summary>
internal static class KeyValueLookup
{
    public static bool TryFind(KVObject parent, string key, [MaybeNullWhen(false)] out KVObject value)
    {
        foreach (var (candidate, child) in parent)
        {
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            {
                value = child;
                return true;
            }
        }

        value = default;
        return false;
    }
}
