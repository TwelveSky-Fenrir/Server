namespace Fenrir.Data.Abstractions.Security;

/// <summary>
///     Legacy <c>gmip</c> (<c>MyDB::CheckGMIP</c>), consulted for two distinct purposes. First: an allowlisted
///     IP always bypasses <see cref="IBlockedIpRepository" />/<see cref="IFirewallRuleRepository" /> in
///     <see cref="ApplicationFirewall" />, so an operator's own known-good IP is never caught by a block rule
///     meant for someone else. Second, the actual legacy <c>CheckGMIP</c> semantics (an elevated/GM-tier
///     account, <c>uUserSort != 0</c>, may only log in from an allow-listed IP,
///     Server/ts25login/S04_MyWork02.cpp:192-201 and S08_MyDB.cpp:339-356): enforced directly in
///     <c>Fenrir.Application.Login.Services.Login.LoginService.LoginAsync</c>, gated on
///     <c>AuthenticateAccountDto.AccountGrade</c> (added by <c>Database/Migrations/011_accounts_add_account_grade.sql</c>)
///     the same way <c>DeviceSpoofingGuard</c> gates its own GM exemption.
/// </summary>
public interface IGmAllowlistRepository
{
    public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct);

    /// <summary>Throws if <paramref name="ipAddress" /> is already allowlisted (usp_GmAllowlist_Add THROW 50304).</summary>
    public ValueTask<int> AddAsync(string ipAddress, CancellationToken ct);
}
