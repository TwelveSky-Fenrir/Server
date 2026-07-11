namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     op23 Scroll of Seekers family (world.Items 1124/1187/7016/8409/8410) -- adds a fixed per-item amount to
///     the character's banked <c>aZone126Time</c> zone-time counter
///     (<see cref="World.PlayerRuntimeState.ScrollOfSeekersTime" />), ceiling-checked via the same narrow
///     <c>wCheckAdd</c>-style 32-bit add <see cref="DungeonAccessTicketResolver" />/<see cref="CashTimerResolver" />
///     already use for their own families (<see cref="BankedCounterMath.AddNarrow" />). Modeled as its own
///     resolver rather than folded into either sibling type: this family is not a dungeon-access ticket (so it
///     does not belong in <see cref="DungeonAccessTicketResolver" />), and it is not one of
///     <see cref="CashTimerResolver" />'s own two already-scoped ids (Faction Notice Scroll/Taiyan Key) -- that
///     type's own remarks explicitly defer "the rest of the family," which this workstream's own fresh
///     legacy-behavior-translator pass has now separately resolved for this specific family only.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3847-3867 (the full case block: the outer five-id dispatch
///     list, a <c>tAddTime</c> default of 180, a nested three-id override raising it to 900, the credit to
///     <c>aZone126Time</c>, the cash-item-use log call, and the single-unit <c>DecreaseQunatity</c> consumption
///     with no <c>r-&gt;tValue</c> echo -- confirmed this session per the recovered
///     <c>scroll-of-seekers-per-id-split</c> contract) ; :106-111 (<c>wCheckAdd</c>, the overflow guard against
///     <c>MAX_NUMBER_SIZE</c>, its shared use-item-fail response, and its early return before any state change) ;
///     Server/Header/Protocol/DEFINE.h:365 (<c>MAX_NUMBER_SIZE</c> = 2,000,000,000, the ceiling <c>wCheckAdd</c>
///     guards against) ; Server/ts25zone/S04_MyWork03.cpp:613-628 (<c>DecreaseQunatity</c> -- single-unit,
///     non-bulk consumption, corroborating the non-bulk-aware behavior this family's handler applies) ;
///     Server/Header/use_inventory.h:31 (<c>WUSE_ITEM_1124</c>, an unconditional top-level <c>#define</c>, no
///     <c>#ifdef</c> gate -- confirmed live in every build; a repo-wide grep finds no <c>#undef</c> anywhere in
///     the compiled source tree, only echoes inside the non-compiled <c>S04_MyWork03.cpp.bak_999</c> backup
///     file, which is not referenced by any <c>.vcxproj</c>).
///     <para>
///         A client-supplied bulk/quantity count (the wire request's "Value" field) is deliberately never read
///         here, matching the cited branch's own asymmetry with the immediately preceding item 828/837 case
///         (which does read a bulk count and does echo its resulting counter total): this family always credits
///         exactly one flat amount and consumes exactly one unit, regardless of what bulk count the client
///         sends, and the response never echoes the new <c>aZone126Time</c> total the way DungeonKey/Elite
///         Dungeon/Ivy Hall do -- see this family's own handler for the "no Value echo" response shape.
///     </para>
/// </remarks>
public static class ScrollOfSeekersResolver
{
    public enum Outcome
    {
        Success,

        /// <summary>Adding would exceed the shared 2,000,000,000 ceiling -- clean failure, counter untouched.</summary>
        WouldExceedCeiling
    }

    /// <summary>"Scroll of Seekers" -- the default 180 amount, not one of the three ids overridden to 900.</summary>
    public const int ScrollOfSeekersItemId = 1124;

    /// <summary>"Scroll of Seekers(L)" -- overridden to 900.</summary>
    public const int ScrollOfSeekersLItemId = 1187;

    /// <summary>"Scroll of Seekers(15h)" -- overridden to 900.</summary>
    public const int ScrollOfSeekers15HourItemId = 7016;

    /// <summary>"Scroll of Seekers(3h)" -- the default 180 amount, not one of the three ids overridden to 900.</summary>
    public const int ScrollOfSeekers3HourItemId = 8409;

    /// <summary>
    ///     "Scroll of Seekers(15h)" -- a second, distinct catalog id sharing the same display name as
    ///     <see cref="ScrollOfSeekers15HourItemId" />; also overridden to 900.
    /// </summary>
    public const int ScrollOfSeekers15HourAltItemId = 8410;

    /// <summary>The five ids the legacy dispatch's outer case list claims for this family.</summary>
    public static IEnumerable<int> HandledItemIds { get; } =
    [
        ScrollOfSeekersItemId,
        ScrollOfSeekersLItemId,
        ScrollOfSeekers15HourItemId,
        ScrollOfSeekers3HourItemId,
        ScrollOfSeekers15HourAltItemId
    ];

    public const int DefaultAddAmount = 180;
    public const int OverrideAddAmount = 900;

    /// <summary>
    ///     The flat amount credited for <paramref name="itemId" /> -- 900 for the inner override subset
    ///     (1187/7016/8410), 180 for every other id this family claims (1124/8409). Returns the 180 default for
    ///     any id outside this family too, matching the legacy's own unconditional <c>tAddTime = 180</c>
    ///     default assignment before the override switch runs -- callers are expected to only invoke this for
    ///     an id already confirmed to be a member of <see cref="HandledItemIds" />.
    /// </summary>
    public static int AmountFor(int itemId)
    {
        return itemId switch
        {
            ScrollOfSeekersLItemId or ScrollOfSeekers15HourItemId or ScrollOfSeekers15HourAltItemId =>
                OverrideAddAmount,
            _ => DefaultAddAmount
        };
    }

    public static Result Resolve(int itemId, int currentZoneTime)
    {
        var amount = AmountFor(itemId);
        var added = BankedCounterMath.AddNarrow(currentZoneTime, amount);
        return added.Succeeded
            ? new Result(Outcome.Success, amount, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, amount, currentZoneTime);
    }

    public readonly record struct Result(Outcome Outcome, int CreditedAmount, int NewZoneTime)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
