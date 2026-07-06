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

    /// <summary>Checked once at world entry, never re-queried per action -- mirrors IMuteRepository's own usage.</summary>
    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);

    /// <summary>
    ///     Creates one new admin.Bans row (usp_Ban_Create) and returns its BanId. Not idempotent -- every call inserts
    ///     a fresh row, even a repeat ban of the same target (this is a log, not a single-row-per-target flag).
    ///     <paramref name="accountId" />/<paramref name="characterId" /> are independently nullable (legacy's own
    ///     uUserIdx/uCharIdx targeting), but at least one must be non-null (CK_Bans_AccountOrCharacter backstops this
    ///     server-side; usp_Ban_Create itself throws 50301 first).
    /// </summary>
    public ValueTask<int> CreateAsync(int? accountId, int? characterId, BanReason reason, DateTime? expiresAtUtc,
        CancellationToken ct);
}
