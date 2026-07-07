namespace Fenrir.Data.Abstractions.Security;

/// <summary>
///     admin.Bans (the ban log -- see <c>usp_Ban_Create</c>'s own remarks for why it's a log, not a single-row
///     flag). The two read checks are consulted at the login/world-entry choke points; <see cref="CreateAsync" />
///     is the GM-tooling create path (legacy case 519 "[GM]-BLOCK", Server/ts25zone/S04_MyWork04.cpp:1487-1515).
/// </summary>
public interface IBanRepository
{
    /// <summary>Checked at authentication, in addition to the fast auth.Accounts.IsBanned flag (§ BanRepository remarks).</summary>
    public ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Checked once at world entry, never re-queried per action while the character stays online. Unlike
    ///     <c>IMuteRepository</c> (which <c>MuteRefreshPollHost</c> now re-batches on a fixed interval for
    ///     every online character -- see that host's own remarks), a mid-session GM ban has no periodic
    ///     refresh: it only takes effect on the character's next world entry.
    /// </summary>
    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);

    /// <summary>
    ///     Creates one new admin.Bans row (usp_Ban_Create) and returns its BanId. Not idempotent -- every call inserts
    ///     a fresh row, even a repeat ban of the same target (this is a log, not a single-row-per-target flag).
    ///     <paramref name="accountId" />/<paramref name="characterId" /> are independently nullable (legacy's own
    ///     uUserIdx/uCharIdx targeting), but at least one must be non-null (CK_Bans_AccountOrCharacter backstops this
    ///     server-side; usp_Ban_Create itself throws 50301 first).
    /// </summary>
    /// <param name="actorAccountId">
    ///     The account id of the GM who issued this ban (Migrations/035_bans_actor_attribution.sql). Independently
    ///     nullable from <paramref name="actorCharacterId" /> for the same reason the target pair is; defaults to
    ///     <see langword="null" /> for a ban with no known actor (e.g. a future automated/system-imposed ban) --
    ///     omitting it is a valid call, not an error. Appended trailing so every existing caller keeps compiling.
    /// </param>
    /// <param name="actorCharacterId">
    ///     The character id of the GM who issued this ban -- see <paramref name="actorAccountId" />
    ///     .
    /// </param>
    public ValueTask<int> CreateAsync(int? accountId, int? characterId, BanReason reason, DateTime? expiresAtUtc,
        CancellationToken ct, int? actorAccountId = null, int? actorCharacterId = null);
}
