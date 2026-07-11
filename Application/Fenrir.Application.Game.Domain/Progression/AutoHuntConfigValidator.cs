using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Server-side validation of the 112-byte client-supplied <c>AUTO_HUNT</c> configuration blob
///     (<c>CZ_AUTO_CONFIG_SEND</c>, <c>Server/ts25zone/S04_MyWork02.cpp:13466-13616</c>). Closes the
///     security-hardening target this workstream owns: legacy copies the blob into stored server state verbatim
///     with <b>no field-level validation</b> (<c>S04_MyWork02.cpp:13612</c>) and then trusts its contents from
///     the per-tick bot upkeep, re-validating only the buff-skill <i>grade</i> at use time
///     (<c>S07_MyGame04.cpp:2333-2336</c>). Per this project's "harden, never reproduce" policy, the blind
///     wholesale copy must never be reproduced -- every field is checked here before it is stored or consumed.
/// </summary>
/// <remarks>
///     <b>Conservative by design:</b> this rejects only values that can never be legitimate, so it can never
///     wrongly disconnect a well-behaved client (rejection on this path is a hard disconnect, matching legacy's
///     own malformed-input handling). The two bounds it enforces are both defensible without inventing magnitudes
///     the contract does not supply:
///     <list type="bullet">
///         <item>
///             every skill id, skill grade, and non-flag selector/count field is non-negative (a negative is
///             never a valid id/grade/count/selector);
///         </item>
///         <item>
///             the four command fields the contract explicitly calls "flags" (inventory-command, death-command,
///             pet-prey-command, pet-food-command) are in the boolean domain {0, 1}.
///         </item>
///     </list>
///     <b>Deliberately deferred (no cited bounds -- not invented):</b> tighter upper bounds on
///     <c>BuffType</c>/<c>HuntType</c>/<c>ItemType</c> selectors and the <c>MonNum</c> target count, and full
///     skill-id legality/ownership (each non-zero configured buff/attack skill id actually being a skill this
///     character has learned). The buff-skill grade is already re-clamped to the character's server-side maximum
///     at use time by <c>AutoHuntTickSystem</c> (matching legacy <c>:2333-2336</c>), and Fenrir performs no
///     server-side auto-attack at all, so an out-of-range grade or a fabricated attack-skill id in the stored
///     blob has no server-authoritative effect today; the ownership check is defence-in-depth to add once the
///     learned-skill set is threaded to the call site. See this workstream's open questions.
/// </remarks>
public static class AutoHuntConfigValidator
{
    public enum Rejection
    {
        None,

        /// <summary>A buff-store or attack-type array is the wrong length or null (corrupted blob).</summary>
        MalformedShape,

        /// <summary>A configured skill id (buff-store or attack-type) is negative.</summary>
        NegativeSkillId,

        /// <summary>A configured skill grade (buff-store or attack-type) is negative.</summary>
        NegativeGrade,

        /// <summary>A selector/count field (<c>BuffType</c>/<c>HuntType</c>/<c>ItemType</c>/<c>MonNum</c>) is negative.</summary>
        NegativeSelectorOrCount,

        /// <summary>A command flag is outside the boolean domain {0, 1}.</summary>
        FlagOutOfDomain
    }

    /// <summary>8 buff-store entries, each a (skillId, grade) pair -- <c>Server/Header/Protocol/STRUCT.h:306-322</c>.</summary>
    private const int BuffStoreLength = 16;

    /// <summary>2 attack-type entries, each a (skillId, grade) pair.</summary>
    private const int AttackTypeLength = 4;

    /// <summary>
    ///     True when the blob is safe to store/consume; <see cref="Result.Rejection" /> names the first violation
    ///     otherwise.
    /// </summary>
    public static Result Validate(in AutoHunt config)
    {
        var buffStore = config.BuffStore;
        var attackType = config.AttackType;
        if (buffStore is null || buffStore.Length != BuffStoreLength ||
            attackType is null || attackType.Length != AttackTypeLength)
            return new Result(false, Rejection.MalformedShape);

        // Buff-store and attack-type both interleave (skillId, grade): even indices = skill id, odd = grade.
        for (var i = 0; i < BuffStoreLength; i += 2)
        {
            if (buffStore[i] < 0)
                return new Result(false, Rejection.NegativeSkillId);
            if (buffStore[i + 1] < 0)
                return new Result(false, Rejection.NegativeGrade);
        }

        for (var i = 0; i < AttackTypeLength; i += 2)
        {
            if (attackType[i] < 0)
                return new Result(false, Rejection.NegativeSkillId);
            if (attackType[i + 1] < 0)
                return new Result(false, Rejection.NegativeGrade);
        }

        if (config.BuffType < 0 || config.HuntType < 0 || config.ItemType < 0 || config.MonNum < 0)
            return new Result(false, Rejection.NegativeSelectorOrCount);

        if (!IsFlag(config.InvenCmd) || !IsFlag(config.DeathCmd) || !IsFlag(config.AnimalPreyCmd) ||
            !IsFlag(config.AnimalFoodCmd))
            return new Result(false, Rejection.FlagOutOfDomain);

        return new Result(true, Rejection.None);
    }

    private static bool IsFlag(int value)
    {
        return value is 0 or 1;
    }

    public readonly record struct Result(bool IsValid, Rejection Rejection);
}
