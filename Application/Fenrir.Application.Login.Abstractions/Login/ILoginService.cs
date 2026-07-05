using System.Collections.Immutable;
using System.Net;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Abstractions.Login;

public enum LoginOutcome
{
    /// <summary>Silent drop, no reply/abort: a legitimate NAT-shared client that burst its IP budget just retries later.</summary>
    RateLimited,
    Failure,
    Success
}

/// <summary>
///     <paramref name="ReArmVersionOk" /> mirrors the legacy quirk where only a full authentication attempt
///     (account lookup + password verify) re-arms VersionOk so the client can retry on the same connection --
///     the earlier gates (IP block/version/MAC ban) never re-arm it.
/// </summary>
public sealed record LoginResult(
    LoginOutcome Outcome,
    int ResultCode,
    string ResultString,
    bool ReArmVersionOk,
    int AccountId,
    bool RequirePin,
    string PinMask,
    ImmutableArray<CharacterSummaryDto> Characters)
{
    public static LoginResult RateLimitedResult { get; } =
        new(LoginOutcome.RateLimited, 0, "", false, 0, false, "", []);
}

public interface ILoginService
{
    public ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken);
}
