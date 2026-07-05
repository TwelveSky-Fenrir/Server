namespace Fenrir.Data.Security;

/// <summary>Legacy <c>blockip</c> (<c>MyDB::CheckBanIP</c>) -- a flat, always-enforced IP deny-list.</summary>
public interface IBlockedIpRepository
{
    /// <summary>Hits the natively-compiled admin.usp_BlockedIp_Exists directly: it's meant for exactly this, once per connection attempt.</summary>
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct);
}
