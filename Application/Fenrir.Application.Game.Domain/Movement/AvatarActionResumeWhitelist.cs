using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Movement;

/// <summary>
///     CZ_UPDATE_AVATAR_ACTION (op16)'s own inline Sort/Type legality switch -- entirely separate from
///     <see cref="CharacterMotionWhitelist" />, which gates CZ_AVATAR_ACTION_SEND (op15) exclusively.
///     Evaluated once per accepted op16 packet (<c>Zone.PlayerLifecycle</c>'s <c>HandleMove</c>) ahead of
///     any of that action's own effects. A pure table lookup: no I/O, no shared state, safe to call from
///     any thread.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:1789-1922 (full op16 handler body), specifically
///     :1815-1878 (this Sort/Type switch, the subject reimplemented here) and :1875-1877 (illegal-Sort
///     disconnect).
///     <para>
///         Legal Sort set: 1, 2, 11, 19, 31, 32, 64, 90, 91, 92, 93, 94, 95. Sort 11 is reachable only under
///         <c>USE_REGULAR_WAR_AFK_TICK</c>, itself derived unconditionally from a flag active in the shipped
///         build variant (<c>H01_MainApplication.h:20-22</c>) -- live, not dead, code, so it is included
///         here unconditionally rather than gated behind a runtime flag Fenrir has no equivalent of.
///     </para>
///     <para>
///         Only Sort 19, 31, and 64 additionally require Type == 0 (:1827-1849); every other legal Sort
///         accepts any Type 0-7 unconditionally, including Sort 90/91/92 (no Type check coded at all,
///         :1850-1853) and Sort 93/94/95 (their own companion Type checks are present in source but
///         commented out/disabled, :1854-1874).
///     </para>
///     <para>
///         This table must never be conflated with, or substituted by, the op15-only
///         <see cref="CharacterMotionWhitelist" /> (<c>CheckValidCharacterMotionForSend</c>) -- that routine
///         has exactly one call site in the whole legacy codebase, inside op15's own handler
///         (S04_MyWork02.cpp:1544). Reusing it for op16 traffic previously caused legitimate op16 packets
///         carrying Sort 19, 31, 92, 93, 94, or 95 (several tied to the fishing state machine -- the server's
///         own outgoing assignment of these exact Sort values before broadcast, S04_MyWork02.cpp:13810
///         (92), :13885 (93), :13949 (94), :13953 (95)) to be wrongly hard-disconnected, since op15's table
///         has no row for them at all; and narrowed the legal Type range for Sort 90/91 versus what op16's
///         own switch actually allows.
///     </para>
///     <para>
///         Out of scope for this table: the other side effects the legacy op16 handler performs alongside
///         this Sort/Type gate (AFK-tick reset for Sort 11, the fishing "catching" flag clear for Sort
///         94/95, the post-acceptance skill-grade anti-hack check, and party-buff-state transitions for a
///         subset of skill numbers, :1801-1920 generally) are not reimplemented here and are tracked
///         separately -- this class reproduces only the Sort/Type legality boundary itself, which is what
///         the character-motion-whitelist-reverify finding is about.
///     </para>
///     <para>
///         "Create effect value" processing for Sort 1 (:1818, <c>ProcessForCreateEffectValue</c>) IS now
///         reimplemented -- per the skill-casting-cooldown-mechanics behavior contract, it lives in
///         <c>Zone.PlayerLifecycle</c>'s <c>ApplySkillEffectConfirm</c>, called from <c>HandleMove</c> when
///         this table accepts an op16 action whose Sort equals 1, not here: this table's own responsibility
///         stays limited to the Sort/Type legality boundary.
///     </para>
/// </remarks>
public static class AvatarActionResumeWhitelist
{
    // Type-value bitmasks: bit N set means Type N is one of the values a Sort legalizes -- same bit-per-Type
    // encoding as CharacterMotionWhitelist for consistency, not a shared table.
    private const byte Type0 = 0b0000_0001;
    private const byte AllTypes = 0b1111_1111;

    private static readonly FrozenDictionary<int, byte> TypeMasksBySort = new Dictionary<int, byte>
    {
        [1] = AllTypes,
        [2] = AllTypes,
        [11] = AllTypes,
        [19] = Type0,
        [31] = Type0,
        [32] = AllTypes,
        [64] = Type0,
        [90] = AllTypes,
        [91] = AllTypes,
        [92] = AllTypes,
        [93] = AllTypes,
        [94] = AllTypes,
        [95] = AllTypes
    }.ToFrozenDictionary();

    /// <summary>
    ///     Resolves <paramref name="sort" />/<paramref name="type" /> against op16's own legality switch.
    /// </summary>
    /// <param name="sort">The avatar action's category ("aSort"). Any value not in the table is illegal.</param>
    /// <param name="type">The avatar action's sub-variant ("aType"), 0-7. Legal values depend on <paramref name="sort" />.</param>
    /// <returns>
    ///     False means the pair is illegal -- the caller must disconnect the whole client session
    ///     (S04_MyWork02.cpp:1875-1877), the same "hostile client" sanction op15's own whitelist uses.
    /// </returns>
    public static bool IsLegal(int sort, int type)
    {
        return type is >= 0 and <= 7 && TypeMasksBySort.TryGetValue(sort, out var mask) &&
               (mask & (1 << type)) != 0;
    }
}
