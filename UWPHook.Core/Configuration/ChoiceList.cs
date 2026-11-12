namespace UWPHook.Core.Configuration;

/// <summary>
/// The values a Settings combo box offers, in display order. Settings stores the chosen value,
/// and the combo box works with its index.
/// </summary>
public sealed class ChoiceList
{
    private const string AnyValue = "any";

    public ChoiceList(params string[] values) => Values = values;

    public IReadOnlyList<string> Values { get; }

    /// <summary>The index of <paramref name="value"/>, ignoring case and spaces; an unknown value is "any".</summary>
    public int IndexOf(string value)
    {
        var index = Find(value);
        return index >= 0 ? index : Math.Max(0, Find(AnyValue));
    }

    /// <summary>The value at <paramref name="index"/>; no selection (-1) or an invalid index keeps <paramref name="current"/>.</summary>
    public string ValueAt(int index, string current) => IsValid(index) ? Values[index] : current;

    /// <summary>The value at <paramref name="index"/>, or "any" outside the list.</summary>
    public string ValueAtOrAny(int index) => ValueAt(index, AnyValue);

    private bool IsValid(int index) => index >= 0 && index < Values.Count;

    private int Find(string value)
    {
        var trimmed = value.Trim();
        for (var i = 0; i < Values.Count; i++)
            if (string.Equals(Values[i], trimmed, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}

/// <summary>The SteamGridDB artwork options in Settings, with the values the API takes.</summary>
public static class ArtworkChoices
{
    public static ChoiceList Styles { get; } = new("any", "alternate", "blurred", "white_logo", "material", "no_logo");

    public static ChoiceList Types { get; } = new("any", "static", "animated");

    public static ChoiceList Nsfw { get; } = new("false", "any", "true");

    public static ChoiceList Humor { get; } = new("false", "any", "true");
}
