using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UWPHook.Core.Configuration;

/// <summary>
/// Reads and writes <see cref="AppSettings"/> as indented camelCase JSON. The SteamGridDB API key is
/// written only protected (<c>steamGridDbApiKeyProtected</c>). Loading never throws: a missing file gives
/// the defaults, a corrupt one is moved aside (<c>settings.json.bad</c>) and gives the defaults too, and one
/// that cannot be read right now is left alone and gives the defaults for this run.
/// </summary>
public sealed class SettingsStore(string path, ISecretProtector protector, TimeProvider? time = null)
{
    private readonly TimeProvider clock = time ?? TimeProvider.System;

    public string FilePath => path;

    /// <summary>True when a damaged settings file was moved aside (<c>settings.json.bad*</c>) at some point.</summary>
    public bool HasMovedAsideFile
    {
        get
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
            return Directory.Exists(folder) && Directory.EnumerateFiles(folder, Path.GetFileName(path) + ".bad*").Any();
        }
    }

    private const string ProtectedKeyProperty = "steamGridDbApiKeyProtected";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>
    /// Called when the file could not be used, with where it was moved: a path for a corrupt file, null for
    /// one that could not be read (locked, access denied), which is left untouched.
    /// </summary>
    public Action<Exception, string?>? OnUnreadableFile { get; set; }

    /// <summary>
    /// Called when the stored API key cannot be decrypted (it was protected for another Windows user or machine).
    /// The key loads as empty, and saving keeps the stored key until a new one is saved.
    /// </summary>
    public Action<Exception>? OnUndecryptableKey { get; set; }

    public bool Exists => File.Exists(path);

    // The protected key from the last Load that could not be decrypted, written back by Save while the key is empty.
    private string? undecryptableKey;

    /// <summary>Never throws, also when a handler throws.</summary>
    public AppSettings Load()
    {
        if (!Exists) return AppSettings.Defaults;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not corrupt, just not readable now: keep it for the next run.
            Notify(() => OnUnreadableFile?.Invoke(e, null));
            return AppSettings.Defaults;
        }

        try
        {
            return Parse(text);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            var movedTo = MoveAside();
            Notify(() => OnUnreadableFile?.Invoke(e, movedTo));
            return AppSettings.Defaults;
        }
    }

    /// <summary>
    /// Atomic: writes a temp file of its own (<c>settings.json.&lt;guid&gt;.tmp</c>, so two processes never share one)
    /// next to the file, flushes it to disk, then moves it over the file.
    /// </summary>
    public void Save(AppSettings settings)
    {
        var text = Serialize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            WriteThrough(temp, text);
            File.Move(temp, path, overwrite: true);
            if (settings.HasSteamGridDbApiKey) undecryptableKey = null;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    /// <summary>Flushed to disk before the move, so a power loss can't leave an empty settings.json.</summary>
    private static void WriteThrough(string file, string text)
    {
        using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private AppSettings Parse(string text)
    {
        undecryptableKey = null;
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
        catch (Exception e)
        {
            undecryptableKey = protectedKey;
            Notify(() => OnUndecryptableKey?.Invoke(e));
            return "";
        }
    }

    private string Serialize(AppSettings settings)
    {
        var document = JsonSerializer.SerializeToNode(settings, Json)!.AsObject();
        document[ProtectedKeyProperty] = settings.HasSteamGridDbApiKey
            ? protector.Protect(settings.SteamGridDbApiKey)
            : undecryptableKey ?? "";
        return document.ToJsonString(Json);
    }

    // A failing handler must not make Load throw; the caller's handler is where logging happens.
    private static void Notify(Action notify)
    {
        try
        {
            notify();
        }
        catch (Exception)
        {
            // Ignored on purpose.
        }
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
            if (File.Exists(target)) target += "-" + clock.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            File.Move(path, target);
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
