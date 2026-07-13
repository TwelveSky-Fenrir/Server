namespace Fenrir.Security;

/// <summary>
/// Marqueur du squelette <c>Fenrir.Security</c> (Lot F1 — Fondations). Home unique de l'anti-flood
/// (<c>IpFloodGuard</c>), du rate-limit par session/opcode (<c>SessionRateLimiter</c>), de la blocklist
/// unifiée (<c>admin.FirewallRules</c> + <c>admin.BlockedIps</c>) et de l'allowlist GM (correctifs F5/F6).
/// Contenu consolidé au Lot F5. Voir <c>Roadmap/FONDATIONS/09_Securite_Firewall_Native.md</c>.
/// </summary>
public static class AssemblyInfo
{
    /// <summary>Nom logique de l'assembly (repère de squelette).</summary>
    public const string Name = "Fenrir.Security";
}
