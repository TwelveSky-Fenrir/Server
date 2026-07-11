namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Shared math for the op23 (<c>CZ_USE_INVENTORY_ITEM_SEND</c>) dungeon-access ticket families: Elite
///     Dungeon Ticket (world.Items 1047/1097/1098), the standalone Dungeon Key (world.Item 1048), and the Ivy
///     Hall Ticket pair (world.Items 553/1219) -- three independent banked counters
///     (<see cref="World.PlayerRuntimeState.EliteDungeonTime" />/<see cref="World.PlayerRuntimeState.DungeonKeyTime" />/
///     <see cref="World.PlayerRuntimeState.IvyHallTicketTime" />) that all share the identical "add a fixed
///     per-item amount, ceiling-checked, single-unit consume" shape. No I/O, no Zone dependency -- the caller
///     resolves which counter/per-id amount applies and hands the already-resolved shape here, the same
///     division of responsibility <see cref="CashTimerResolver" /> already uses for its own two ids.
///     <para>
///         Level-gate note: the originating contract states items 1047/1097/1098 and 1048 each require the
///         character's own level to already meet "a distinct per-item threshold" before a use is accepted (a
///         hard-disconnect-worthy precondition in the legacy source), but does not supply the concrete
///         threshold value for any of the four ids -- unlike the neighboring, already-modeled Taiyan Key
///         (world.Item 1049, <see cref="CashTimerResolver.ResolveTaiyanKey" />), whose own threshold
///         (<c>LevelProgressionCalculator.MaxLevel</c>) IS separately confirmed by a distinct citation. Reusing
///         that same MaxLevel value for these four ids would be a plausible-looking but uncited guess -- this
///         project's policy against inventing a legacy magnitude from analogy alone means that gate is
///         deliberately NOT modeled here -- the corresponding <c>IUseItemHandler</c> implementations
///         (EliteDungeonTicketUseItemHandler/DungeonKeyUseItemHandler) apply no level precondition at all,
///         which is a real, deliberate interim gap (a below-threshold character can currently use these
///         tickets) flagged for a follow-up legacy-behavior-translator pass to supply the real per-id values,
///         not an oversight. The Ivy Hall Ticket pair (553/1219) is NOT listed among the level-gated ids in the
///         originating contract at all, so no such gap exists for it.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3672-3694 (Elite Dungeon Ticket -- fixed 180/120/60 amounts,
///     confirmed this session per the originating C9-tickets-tower contract) ; :3696-3726 (Dungeon Key 1048 --
///     fixed 1 amount, confirmed this session) ; :6787-6807 (Ivy Hall Ticket -- fixed 180/360 amounts, carried
///     forward from the originating contract, not independently re-opened) ; :106-117 (<c>wCheckAdd</c>/
///     <c>wCheckAdd2</c>, the plain 32-bit ceiling-check style this whole op23 handler's generic overflow
///     guard uses, confirmed by the originating contract to be what every one of these families guards its own
///     counter-overflow case through) ; Server/Header/Protocol/DEFINE.h:365 (2,000,000,000 shared ceiling).
/// </remarks>
public static class DungeonAccessTicketResolver
{
    public enum Outcome
    {
        Success,

        /// <summary>Slot's stacked quantity is &lt; 1 -- clean failure (should not be reachable if the slot resolved at all).</summary>
        InsufficientQuantity,

        /// <summary>Adding would exceed the ceiling in effect for this counter -- clean failure, counter untouched.</summary>
        WouldExceedCeiling
    }

    public const int EliteDungeonTicketLargeItemId = 1047;
    public const int EliteDungeonTicketMediumItemId = 1097;
    public const int EliteDungeonTicketSmallItemId = 1098;
    public const int DungeonKeyItemId = 1048;
    public const int IvyHallTicketSmallItemId = 553;
    public const int IvyHallTicketLargeItemId = 1219;

    public const int EliteDungeonTicketLargeAmount = 180;
    public const int EliteDungeonTicketMediumAmount = 120;
    public const int EliteDungeonTicketSmallAmount = 60;
    public const int DungeonKeyAmount = 1;
    public const int IvyHallTicketSmallAmount = 180;
    public const int IvyHallTicketLargeAmount = 360;

    /// <summary>
    ///     <paramref name="ceiling" /> defaults to the shared 2,000,000,000 ceiling
    ///     (<see cref="BankedCounterMath.GlobalCeiling" />) -- every caller except the Ivy Hall Ticket family
    ///     uses that default; Ivy Hall passes its own (documented-placeholder, see this type's own remarks)
    ///     value explicitly.
    /// </summary>
    public static Result Resolve(int currentCounter, int perUnitAmount, int slotQuantity,
        int ceiling = BankedCounterMath.GlobalCeiling)
    {
        if (slotQuantity < 1)
            return new Result(Outcome.InsufficientQuantity, currentCounter);

        var added = BankedCounterMath.AddNarrow(currentCounter, perUnitAmount, ceiling);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentCounter);
    }

    public readonly record struct Result(Outcome Outcome, int NewCounterValue)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
