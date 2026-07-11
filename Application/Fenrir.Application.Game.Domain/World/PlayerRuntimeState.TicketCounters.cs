namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Banked counters for the op23 (<c>CZ_USE_INVENTORY_ITEM_SEND</c>) dungeon-access ticket family added by
///     workstream C9-tickets-tower on top of the pre-existing Bottle/Guild-Scroll/Faction-Transfer/GP-ticket/
///     Taiyan-Key dispatch (see <see cref="TaiyanKeyTimer" />, the sibling counter for item 1049, already
///     modeled in <c>PlayerRuntimeState.Consumables.cs</c>). Every field here is session-scoped only: the
///     C9-tickets-tower behavior contract that introduced them did not supply a legacy field name (no
///     <c>aXXX</c> identifier is cited for any of the three, unlike <see cref="TaiyanKeyTimer" />'s own
///     confirmed <c>aZone125Time</c>) or a persisted <c>game.Characters</c> column, so — same posture already
///     documented on the protection-charm counters in <c>PlayerRuntimeState.Consumables.cs</c> — these reset to
///     0 on every zone (re)entry until a follow-up adds durable storage. The three counters are deliberately
///     modeled as three DISTINCT fields, not one shared counter: the originating contract's own wording ("its
///     own cumulative dungeon-time counter" / "a different dungeon-time counter" / "yet another zone-time
///     counter" / "its own zone-time counter under a distinct... cap") states each ticket family banks into an
///     independent counter, the same way <see cref="TaiyanKeyTimer" /> is independent from all three.
/// </summary>
public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Elite Dungeon Ticket family (world.Items 1047 Large/1097 Medium/1098 Small) -- a shared banked
    ///     dungeon-time counter, incremented by a fixed per-item amount (180/120/60) on each use. See
    ///     <see cref="Consumables.DungeonAccessTicketResolver" />.
    /// </summary>
    public int EliteDungeonTime { get; set; }

    /// <summary>
    ///     world.Item 1048's own dungeon-time counter -- distinct from <see cref="EliteDungeonTime" /> and from
    ///     <see cref="TaiyanKeyTimer" /> (item 1049's sibling counter), incremented by a fixed 1 per use. See
    ///     <see cref="Consumables.DungeonAccessTicketResolver" />.
    /// </summary>
    public int DungeonKeyTime { get; set; }

    /// <summary>
    ///     Ivy Hall Ticket family (world.Items 553 Small/1219 Large) -- a shared banked zone-time counter,
    ///     incremented by a fixed per-item amount (180/360) on each use. The originating contract states this
    ///     counter is checked against "a distinct, harder [lower] numeric cap than the generic one" but does not
    ///     supply the concrete value; see <see cref="Consumables.DungeonAccessTicketResolver" />'s own remarks
    ///     for why the generic 2,000,000,000 ceiling is used here as a documented, conservative interim
    ///     placeholder rather than an invented tighter number.
    /// </summary>
    public int IvyHallTicketTime { get; set; }

    /// <summary>
    ///     Scroll of Seekers family (world.Items 1124/1187/7016/8409/8410) -- a shared banked zone-time counter,
    ///     incremented by a fixed per-item amount (180 for 1124/8409, 900 for 1187/7016/8410) on each use. See
    ///     <see cref="Consumables.ScrollOfSeekersResolver" />. Unlike the three siblings above, the
    ///     <c>scroll-of-seekers-per-id-split</c> contract DOES independently confirm a real legacy field name
    ///     for this one -- <c>aZone126Time</c>, a DB-persisted <c>int(11)</c> column
    ///     (<c>nxtserver.sql:88</c>/<c>CSQLAvatar.cpp:608</c>) -- but Fenrir has no corresponding
    ///     <c>game.Characters</c> column or write-behind wiring yet, so this field is still session-scoped only
    ///     and resets to 0 on every zone (re)entry, same interim posture as <see cref="EliteDungeonTime" />/
    ///     <see cref="DungeonKeyTime" />/<see cref="IvyHallTicketTime" /> until a follow-up adds durable
    ///     storage.
    /// </summary>
    public int ScrollOfSeekersTime { get; set; }
}
