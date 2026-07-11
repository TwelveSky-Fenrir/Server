namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     Continuation of <see cref="GmActionEventCodes" />'s locally-scoped <c>game.EventLog.EventCode</c> catalog
///     for <see cref="EventLogCategory.GmAction" />, added as a sibling file (not an edit to
///     <see cref="GmActionEventCodes" /> itself) by the "A14-gm-remaining" implementation pass so the two new
///     GM commands below never collide with any of that catalog's existing 1-11 values. Codes 12/13 continue
///     that same numbering -- a future edit pass folding these two constants directly into
///     <see cref="GmActionEventCodes" /> (single catalog file) is a purely mechanical follow-up, not a
///     behavioral change; see this pass's own wiring notes.
/// </summary>
internal static class GmDuelAndInventoryActionEventCodes
{
    /// <summary>
    ///     The Basic-tier (<c>GmCommandTier.Basic</c>) GM-CALLPVP command (tSort 599,
    ///     <see cref="GmCallPvpService" />) relocating one matched connected character -- one row per relocated
    ///     target, mirroring the sibling CALL command's (tSort 514, <see cref="GmActionEventCodes.Call" />) own
    ///     per-target audit shape. This project's own standing rule ("every balance/currency/positional-power GM
    ///     mutation gets an audit record, unconditionally") applies here as a Fenrir-authored addition -- the
    ///     source contract's own citations do not confirm a legacy <c>GL_*</c> call exists for this specific
    ///     command (unlike CALL's own confirmed <c>GL_*</c> call).
    /// </summary>
    public const short CallPvpRelocate = 12;

    /// <summary>
    ///     The Basic-tier (<c>GmCommandTier.Basic</c>) GM_CLEAR_INVENTORY command (tSort 701,
    ///     <see cref="GmClearInventoryService" />) wiping the invoking GM's own inventory page(s). The source
    ///     contract's own citations do not confirm a legacy <c>GL_*</c> call exists for this command either --
    ///     this is a Fenrir-authored addition per the same standing audit-trail rule as
    ///     <see cref="CallPvpRelocate" />, matching how <see cref="GmActionEventCodes.MaxStatCheat" /> and
    ///     <see cref="GmActionEventCodes.PetExperienceGrant" /> already add an audit row for their own
    ///     legacy-silent bodies.
    /// </summary>
    public const short ClearInventory = 13;
}
