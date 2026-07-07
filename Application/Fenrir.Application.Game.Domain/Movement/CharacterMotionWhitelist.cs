using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Movement;

/// <summary>
///     Character-motion (animation) whitelist gating the attack sub-packet budget --
///     <c>CheckValidCharacterMotionForSend</c>. Evaluated once per accepted CZ_AVATAR_ACTION_SEND (op15)
///     packet ONLY (<c>Zone.PlayerLifecycle</c>'s <c>HandleMove</c>), before any of that action's own effects
///     (position/ActionSort write, broadcast, skill/mana deduction). A pure table lookup: no I/O, no shared
///     state, safe to call from any thread.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:4049-4763 (<c>CheckValidCharacterMotionForSend</c>:
///     signature/defaults 4050-4055, full case table 4056-4761, fallthrough <c>return FALSE</c> 4762) ;
///     4249-4252 (Sort 31 is an identically-bodied sibling of Sort 30, reachable only under
///     <c>#ifdef __MOBILE__</c> -- confirmed never defined in any shipped <c>Server/Header</c>/
///     <c>Server/ts25latest_config.props</c> configuration, so it is dead code and NOT a legal category
///     here) ; 4525-4535 and 4615-4623 (Sort 65/74 explicitly clear the attack-budget enforcement flag) ;
///     4043-4047 and Server/ts25zone/S04_MyWork02.cpp:1293-1327 (<c>CheckSkillValidForZone</c>, the unrelated,
///     inert "always TRUE" check that runs immediately before this one on the same packet -- not
///     reimplemented here, it does nothing in the shipped source either) ; S04_MyWork02.cpp:1538-1652 (call
///     site: disconnect on failure at 1544-1548) ; S04_MyWork02.cpp:855-869 and
///     Server/ts25zone/H07_MyGame.h:843-847 (the four legacy session fields these outputs feed, and their
///     "enforced, ceiling zero" state at session start) ; ServerDocs/12_ts25zone/07_MyWork05_Helpers.md §11
///     (independent recount of the case table, cross-checked against the direct read above).
///     <para>
///         CZ_UPDATE_AVATAR_ACTION (op16) does NOT run through this table -- <c>CheckValidCharacterMotionForSend</c>
///         has exactly one call site in the whole legacy codebase, inside op15's own handler (:1544). Op16
///         runs its own, separate inline switch instead (S04_MyWork02.cpp:1815-1878, materially more
///         permissive for several Sorts) -- see <see cref="AvatarActionResumeWhitelist" />. The two tables
///         must never be conflated: doing so previously caused legitimate op16 packets to be wrongly
///         hard-disconnected for Sort values (19, 31, 92-95) that only op16's own table legalizes.
///     </para>
/// </remarks>
public static class CharacterMotionWhitelist
{
    // Type-value bitmasks: bit N set means Type N is one of the values a row legalizes.
    private const byte Type0 = 0b0000_0001;
    private const byte Type3 = 0b0000_1000;
    private const byte Type5 = 0b0010_0000;
    private const byte Type7 = 0b1000_0000;
    private const byte AllTypes = 0b1111_1111;
    private const byte EvenTypes = 0b0101_0101; // 0, 2, 4, 6
    private const byte OddTypes = 0b1010_1010; // 1, 3, 5, 7

    private static readonly FrozenDictionary<int, Rule> Rules = new Dictionary<int, Rule>
    {
        [0] = new(Type0, 0, true, 0, 0),
        [1] = new(AllTypes, 0, true, 0, 0),
        [2] = new(AllTypes, 0, true, 0, 0),
        [3] = new(EvenTypes, 0, true, 0, 0),
        [4] = new(OddTypes, 0, true, 0, 0),
        [5] = new(OddTypes, 0, true, 1, 1),
        [6] = new(OddTypes, 0, true, 1, 2),
        [7] = new(OddTypes, 0, true, 1, 3),
        // Sort 8 has no row: 8 is not a legal action category.
        [9] = new(OddTypes, 0, true, 0, 0),
        [10] = new(AllTypes, 0, true, 0, 0),
        // 11-12 have no row (11 = stun, a server-applied state per PlayerRuntimeState.IsStunned's own
        // remarks, never a client-originated Sort here); 30/31 dead-sibling note below applies to 31, not
        // this gap.
        [13] = new(Type0, 0, true, 0, 0),
        [14] = new(Type0, 0, true, 0, 0),
        [15] = new(Type0, 0, true, 0, 0),
        [16] = new(Type0, 0, true, 0, 0),
        [17] = new(Type0, 0, true, 0, 0),
        [18] = new(Type0, 0, true, 0, 0),
        [20] = new(Type0, 0, true, 0, 0),
        [21] = new(Type0, 0, true, 0, 0),
        [22] = new(Type0, 0, true, 0, 0),
        [23] = new(Type0, 0, true, 0, 0),
        [30] = new(Type0, 1, true, 0, 0),
        [32] = new(AllTypes, 2, true, 0, 0),
        [33] = new(Type0, 2, true, 0, 0),
        [38] = new(Type0, 2, true, 2, 1),
        [39] = new(Type0, 2, true, 2, 1),
        [40] = new(Type0, 2, true, 0, 0),
        [41] = new(Type0, 2, true, 0, 0),
        [42] = new(Type3, 2, true, 3, 1),
        [43] = new(Type3, 2, true, 3, 3),
        [44] = new(Type3, 2, true, 3, 5),
        [45] = new(Type3, 2, true, 3, 1),
        [46] = new(Type3, 2, true, 3, 3),
        [48] = new(Type5, 2, true, 3, 1),
        [49] = new(Type5, 2, true, 3, 3),
        [50] = new(Type5, 2, true, 3, 5),
        [51] = new(Type5, 2, true, 3, 1),
        [52] = new(Type5, 2, true, 3, 3),
        [54] = new(Type7, 2, true, 4, 1),
        [55] = new(Type7, 2, true, 4, 3),
        [56] = new(Type7, 2, true, 3, 5),
        [57] = new(Type7, 2, true, 4, 1),
        [58] = new(Type7, 2, true, 4, 3),
        [60] = new(Type3, 2, true, 0, 0),
        [61] = new(Type5, 2, true, 0, 0),
        [62] = new(Type7, 2, true, 0, 0),
        [63] = new(Type0, 2, true, 0, 0),
        [64] = new(Type0, 0, true, 0, 0),
        [65] = new(Type0, 0, false, 5, 0),
        [66] = new(Type0, 2, true, 0, 0),
        [67] = new(Type0, 2, true, 0, 0),
        [68] = new(Type0, 2, true, 0, 0),
        [69] = new(Type3, 2, true, 3, 1),
        [70] = new(Type3, 2, true, 3, 3),
        [71] = new(Type5, 2, true, 3, 1),
        [72] = new(Type5, 2, true, 3, 3),
        [73] = new(Type7, 2, true, 4, 3),
        [74] = new(Type7, 2, false, 0, 0),
        [75] = new(Type0, 2, true, 0, 0),
        [76] = new(Type0, 2, true, 0, 0),
        [81] = new(Type3, 2, true, 3, 5),
        [82] = new(Type5, 2, true, 3, 5),
        [83] = new(Type7, 2, true, 3, 5),
        [85] = new(Type3, 2, true, 3, 1),
        [86] = new(Type3, 2, true, 3, 3),
        [87] = new(Type5, 2, true, 3, 1),
        [88] = new(Type5, 2, true, 3, 3),
        [89] = new(Type7, 2, true, 4, 1),
        [90] = new(Type7, 2, true, 4, 3),
        [91] = new(Type0, 0, true, 0, 0),
        // Sentinel: a generic catch-all bucket carrying no attack budget of its own, but with its own
        // distinct skill-category code.
        [255] = new(AllTypes, 3, true, 0, 0)
    }.ToFrozenDictionary();

    /// <summary>
    ///     Resolves <paramref name="sort" />/<paramref name="type" /> against the whitelist.
    /// </summary>
    /// <param name="sort">
    ///     The avatar action's category ("Sort"). Any value not in the table -- including any
    ///     negative value -- is illegal.
    /// </param>
    /// <param name="type">
    ///     The avatar action's sub-variant ("Type"), 0-7. Legal values depend entirely on
    ///     <paramref name="sort" />.
    /// </param>
    /// <param name="evaluation">
    ///     The resolved outputs when this returns true. Meaningless (default) when this returns false.
    /// </param>
    /// <returns>
    ///     False means the pair is illegal -- the caller must disconnect the whole client session, not merely
    ///     reject the one action (the same "hostile client" sanction pattern used throughout this class of
    ///     handler).
    /// </returns>
    public static bool TryEvaluate(int sort, int type, out CharacterMotionEvaluation evaluation)
    {
        if (type is >= 0 and <= 7 && Rules.TryGetValue(sort, out var rule) && (rule.TypeMask & (1 << type)) != 0)
        {
            evaluation = new CharacterMotionEvaluation(rule.SkillCategoryCode, rule.AttackBudgetEnforced,
                rule.AttackFamilyTag, rule.AttackSubPacketCeiling);
            return true;
        }

        evaluation = default;
        return false;
    }

    private readonly record struct Rule(
        byte TypeMask,
        int SkillCategoryCode,
        bool AttackBudgetEnforced,
        int AttackFamilyTag,
        int AttackSubPacketCeiling);
}
