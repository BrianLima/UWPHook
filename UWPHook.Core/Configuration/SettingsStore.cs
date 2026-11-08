using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UWPHook.Core.Configuration;

/// <summary>
/// Reads and writes <see cref="AppSettings"/> as indented camelCase JSON. The SteamGridDB API key is
/// written only protected (<c>steamGridDbApiKeyProtected</c>). Loading never throws: a missing file gives
/// the defaults, and an unreadable one is moved aside (<c>settings.json.bad</c>) and gives the defaults too.
/// </summary>
public sealed class SettingsStore(string path, ISecretProtector protector)
{
    private const string ProtectedKeyProperty = "steamGridDbApiKeyProtected";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>Called when the file could not be read, with where it was moved (null if moving it failed too).</summary>
    public Action<Exception, string?>? OnUnreadableFile { get; set; }

    public bool Exists => File.Exists(path);

    public AppSettings Load()
    {
        if (!Exists) return AppSettings.Defaults;

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            OnUnreadableFile?.Invoke(e, MoveAside());
            return AppSettings.Defaults;
        }
    }

    /// <summary>Atomic: writes <c>settings.json.tmp</c> next to the file, then moves it over the file.</summary>
    public void Save(AppSettings settings)
    {
        var text = Serialize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";

        try
        {
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private AppSettings Parse(string text)
    {
        var document = JsonNode.Parse(text)?.AsObject() ?? throw new JsonException("settings.json is null");
        var settings = document.Deserialize<AppSettings>(Json) ?? throw new JsonException("settings.json is null");
        return settings with { SteamGridDbApiKey = UnprotectedKey(document[ProtectedKeyProperty]) };
    }

    // A key protected for another Windows user or machine cannot be read back: the user enters it again.
    private string UnprotectedKey(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var protectedKey) || protectedKey.Length == 0)
            return "";

        try
        {
            return protector.Unprotect(protectedKey);
        }
        catch (Exception)
        {
            return "";
        }
    }

    private string Serialize(AppSettings settings)
    {
        var document = JsonSerializer.SerializeToNode(settings, Json)!.AsObject();
        document[ProtectedKeyProperty] = settings.HasSteamGridDbApiKey ? protector.Protect(settings.SteamGridDbApiKey) : "";
        return document.ToJsonString(Json);
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The save already failed; that error is the one to report.
        }
    }

    /// <summary>Keeps the unreadable file for the user to look at; never overwrites an earlier one.</summary>
    private string? MoveAside()
    {
        try
        {
            var target = path + ".bad";
            if (File.Exists(target)) target += "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            File.Move(path, target);
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
