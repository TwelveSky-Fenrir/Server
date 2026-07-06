namespace Fenrir.Data.Abstractions.Admin;

/// <summary>
///     The durable, externally-editable store behind the login-time maintenance-lockdown and server-full quota
///     gates (<c>Fenrir.Application.Login.Services.Login.LoginService.LoginAsync</c>) -- an operator edits
///     <c>admin.ServerQuota</c> directly, no redeploy/restart needed, and the running LoginServer picks the new
///     value up on its next recurring re-read (<c>Fenrir.Application.Login.Hosting.ServerQuotaRefreshHost</c>,
///     ~1s) or, at worst, its next restart.
/// </summary>
public interface IServerQuotaRepository
{
    /// <summary>
    ///     The admin.ServerQuota singleton row's MaxPlayers column. A value of exactly zero means "maintenance
    ///     mode", not "zero capacity" -- see the table's own header for the citation.
    /// </summary>
    public ValueTask<int> GetMaxPlayersAsync(CancellationToken ct);
}
