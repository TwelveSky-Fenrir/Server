namespace Fenrir.Data.Security;

/// <summary>
///     Read side of admin.Bans (the ban log -- see <c>usp_Ban_Create</c>'s own remarks for why it's a log, not a
///     single-row flag). Mirrors <c>Fenrir.Data.Admin.IMuteRepository</c>'s shape deliberately: no create/lift
///     method here either, since issuing a ban is a future GM-tooling concern, not something the login/world-entry
///     path itself ever does.
/// </summary>
public interface IBanRepository
{
    /// <summary>Checked at authentication, in addition to the fast auth.Accounts.IsBanned flag (§ BanRepository remarks).</summary>
    public ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct);

    /// <summary>Checked once at world entry, never re-queried per action -- mirrors IMuteRepository's own usage.</summary>
    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);
}
