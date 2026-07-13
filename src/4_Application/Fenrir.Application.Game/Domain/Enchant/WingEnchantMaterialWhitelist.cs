using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Enchant;

/// <summary>
///     Wing (item category <c>iSort == 6</c>) enchant material table and cost constants.
///     Réf. legacy : <c>Server/Header/function.h:2578-2599</c> (<c>CheckWingEnchantMaterial</c>, the accepted
///     material set) and <c>Server/ts25zone/S04_MyWork02.cpp:3035-3079</c> (material -> improve-value table).
///     Materials 2387 and 2392 pass <c>CheckWingEnchantMaterial</c> but carry NO improve value (they fall to
///     the switch default = session disconnect), so they are deliberately absent from
///     <see cref="StandardWingMaterials" /> -- a lookup miss here is a hard reject, which the handler turns
///     into a disconnect, matching legacy. Material 99409 is dead code in ReleaseEU33 (gated on
///     <c>ONLINE_FOR_DS</c>, the never-compiled <c>#else</c> of <c>#ifdef M33</c>) and is intentionally
///     omitted. Material 8106 (safe scroll, under LNW33) has its own no-loss failure path (result code 9)
///     and is out of the standard-material scope handled here.
/// </summary>
public static class WingEnchantMaterialWhitelist
{
    /// <summary>
    ///     Wing enchant scroll (826): success probability is forced to exactly 100 and the wing is filled to
    ///     exactly +40. Réf. <c>Server/Header/function.h:2751-2754</c> (<c>IsWEnchantScroll</c> is true only
    ///     for 826) and <c>Server/ts25zone/S04_MyWork02.cpp:3222-3234</c>.
    /// </summary>
    public const int GuaranteedSuccessScrollItemId = 826;

    /// <summary>
    ///     Safe wing scroll (8106, under LNW33): dedicated no-loss failure path, result code 9. Handled by
    ///     <c>ResolveWingProtectedMaterial</c>; out of the standard-material contract scope.
    /// </summary>
    public const int ProtectedMaterialItemId = 8106;

    public const int ProtectedMaterialFailureResultCode = 9;

    public const int ProtectedMaterialEnchantValue = 1;

    /// <summary>
    ///     Fixed contribution-point cost (field <c>aKillOtherTribe</c>) charged per wing enchant attempt.
    ///     Always consumed, even on failure or destruction; never refunded, never deposited to the tribe bank
    ///     (unlike the normal-item enchant path). Réf. <c>Server/ts25zone/S04_MyWork02.cpp:3081-3100</c>.
    /// </summary>
    public const int WingEnchantCpCost = 50;

    /// <summary>
    ///     Standard wing enchant materials -> nominal improve value gained on success (before the +40 cap).
    ///     826 is nominal +40 and, being the guaranteed-success scroll, always fills the wing to exactly +40.
    ///     Réf. <c>Server/ts25zone/S04_MyWork02.cpp:3035-3079</c>.
    /// </summary>
    public static readonly FrozenDictionary<int, int> StandardWingMaterials = new Dictionary<int, int>
    {
        [695] = 1,
        [696] = 2,
        [698] = 3,
        [2397] = 4,
        [826] = 40
    }.ToFrozenDictionary();
}
