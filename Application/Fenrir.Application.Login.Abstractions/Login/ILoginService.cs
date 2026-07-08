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
    Success,

    /// <summary>
    ///     Silent drop, no reply/abort: this account already held a live session on this same LoginServer process
    ///     (runtime.AccountSessions ConflictLogin) -- the stale local session was just evicted
    ///     (<see cref="Fenrir.Network.Dispatch.Sessions.DisconnectReason.Evicted" />) and this login attempt is
    ///     dropped too, mirroring legacy tResult=8's "the player must retry afterward" rather than the new
    ///     attempt winning outright. See <c>LoginService</c>'s remarks for the ServerDocs citation.
    /// </summary>
    DuplicateSessionEvicted
}

/// <summary>
///     <paramref name="ReArmVersionOk" /> mirrors the legacy quirk where only a full authentication attempt
///     (account lookup + password verify) re-arms VersionOk so the client can retry on the same connection --
///     the earlier gates (IP block/version/MAC ban) never re-arm it.
/// </summary>
/// <param name="SessionToken">
///     Populated only on <see cref="LoginOutcome.Success" /> -- the token
///     <c>usp_AccountSession_ClaimOrSignalKick</c> minted for this login epoch (runtime.AccountSessions), carried
///     into the zone-transfer ticket so the game-side handshake can prove it's completing the same login.
/// </param>
/// <param name="AccountGrade">
///     Populated only on <see cref="LoginOutcome.Success" /> -- the account-grade fact read straight off
///     <c>AuthenticateAccountDto</c> (legacy <c>uUserSort</c>), carried into <c>LoginClientSession.AccountGrade</c>
///     and from there into the zone-transfer ticket, so the Zone session never has to re-query auth.Accounts for it.
/// </param>
/// <param name="GuildNamesByCharacterId">
///     Populated only on <see cref="LoginOutcome.Success" /> -- one entry per <see cref="Characters" /> row that
///     currently belongs to a guild (absent, not empty-string, for a guildless character), resolved live via
///     <c>IGuildRepository.GetByCharacterAsync</c>. Feeds <c>LoginTrain.BuildAvatarSlots</c>' GuildName field; see
///     that method's own remarks for the full citation.
/// </param>
public sealed record LoginResult(
    LoginOutcome Outcome,
    int ResultCode,
    string ResultString,
    bool ReArmVersionOk,
    int AccountId,
    bool RequirePin,
    string PinMask,
    ImmutableArray<CharacterSummaryDto> Characters,
    ImmutableDictionary<int, string> GuildNamesByCharacterId,
    Guid? SessionToken = null,
    short AccountGrade = 0)
{
    public static LoginResult RateLimitedResult { get; } =
        new(LoginOutcome.RateLimited, 0, "", false, 0, false, "", [], ImmutableDictionary<int, string>.Empty);

    /// <summary>Identical wire shape to <see cref="RateLimitedResult" /> -- both are a silent, no-reply drop.</summary>
    public static LoginResult SilentDropResult { get; } =
        new(LoginOutcome.DuplicateSessionEvicted, 0, "", false, 0, false, "", [],
            ImmutableDictionary<int, string>.Empty);
}

public interface ILoginService
{
    public ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken);
}
