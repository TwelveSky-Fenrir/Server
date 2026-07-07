namespace Fenrir.Data.Abstractions.Admin;

/// <summary>
///     The shared, global conversion quota behind the fourth-tribe (Fujin) conversion/return behavior
///     (CZ_CHANGE_TO_TRIBE4_SEND, op37) -- Fenrir's equivalent of the legacy's cross-process
///     MyDB::CheckUpdateSky, backed by the durable <c>admin.TribeFourQuota</c> singleton row instead of a
///     second "extra" process Fenrir does not have. See that table's own header for the fail-safe default
///     (MaxCount=0, no conversions grantable until an operator raises it).
/// </summary>
public interface ITribeFourQuotaRepository
{
    /// <summary>
    ///     Atomically advances the shared counter by one and reports whether doing so stayed within the
    ///     configured cap -- true means this attempt consumed the quota and may proceed; false means the quota
    ///     is exhausted and nothing was changed. Must only be called once every other character-specific
    ///     eligibility gate has already passed -- see
    ///     <c>Fenrir.Application.Game.Domain.Tribes.TribeMigrationOutcome.QuotaExhausted</c>'s own remarks for
    ///     why this ordering is a deliberate hardening deviation from the legacy's own quota-first bug.
    /// </summary>
    public ValueTask<bool> TryConsumeAsync(CancellationToken ct);
}
