namespace UWPHook.Core.Artwork.SteamGridDb;

public enum SgdbStatus
{
    Ok,
    /// <summary>SteamGridDB answered 401: the API key is missing, wrong or revoked.</summary>
    InvalidApiKey,
    NotFound,
    /// <summary>A network, HTTP or JSON error; <see cref="SgdbResult{T}.Error"/> says which.</summary>
    Failed,
}

/// <summary>The outcome of a SteamGridDB request. Errors are values here, never exceptions.</summary>
public sealed class SgdbResult<T>
{
    private readonly T? _value;

    private SgdbResult(SgdbStatus status, T? value, string? error)
    {
        Status = status;
        _value = value;
        Error = error;
    }

    public SgdbStatus Status { get; }

    /// <summary>What went wrong, for <see cref="SgdbStatus.Failed"/>.</summary>
    public string? Error { get; }

    public bool IsOk => Status == SgdbStatus.Ok;

    public T Value => IsOk ? _value! : throw new InvalidOperationException($"A {Status} result has no value.");

    public static SgdbResult<T> Ok(T value) => new(SgdbStatus.Ok, value, null);

    public static SgdbResult<T> InvalidApiKey() => new(SgdbStatus.InvalidApiKey, default, null);

    public static SgdbResult<T> NotFound() => new(SgdbStatus.NotFound, default, null);

    public static SgdbResult<T> Failed(string error) => new(SgdbStatus.Failed, default, error);

    public override string ToString() => Error is null ? Status.ToString() : $"{Status}: {Error}";
}
