namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Holy-Shield absorption and hard-clear over a live <c>BUFF_INFO</c> array (35 slots x [value, duration]) --
///     the pure-arithmetic core of <c>DecreaseBuff</c>/<c>DecreaseHolyShield</c>/<c>RemoveHolyShield</c>
///     (<c>Server/ts25zone/S07_MyGame04.cpp:2614-2749</c>). Mutates the array in place; never touches a
///     <c>Zone</c>/<c>PlayerRuntimeState</c>/socket itself, so the caller owns any broadcast.
/// </summary>
/// <remarks>
///     <para>
///         <b>Base slot 9.</b> The base Holy-Shield slot is BUFF_INFO slot <see cref="BaseSlot" />, written by
///         skill 82 ("Holy Shield") -- see <see cref="Skills.SkillEffectCatalog" /> (skill 82 -&gt; slot 9,
///         <c>ShieldLifeUp</c>, value = ratio% x MaxLife x 0.01, i.e. an absolute HP pool). That slot is
///         write-and-expire only in Fenrir today (no combat code consumed it before this class), so the value
///         stored there IS "the remaining absorbable amount" the source contract describes, with no
///         double-count against MaxLife (<see cref="Stats.StatCalculator" />'s life formula never reads it).
///     </para>
///     <para>
///         <b>The six tiered MG5ORIGIN slots.</b> The live shipped build (<c>MG5ORIGIN</c> defined) resolves
///         absorption by first scanning six tiered shield slots in a fixed order and only falling back to the
///         base slot when none is active; the resolved tier also shifts the broadcast sort code to its own
///         "slot position + 2" (that sort-shift is a separate, still-unmodeled broadcast concern, not this
///         class's own arithmetic). B10 grounds <see cref="TieredSlots" /> to the effect-slot tail (indices
///         29-34, <c>DEFINE.h:331-336</c>'s 35-slot maximum) -- the exact per-slot Holy-Shield-tier identity
///         (which enchant/skill grade writes which of the six) was not independently re-verified against
///         <c>DecreaseBuff</c>/<c>DecreaseHolyShield</c>'s own slot-number literals, only the effect-slot range
///         itself. No Fenrir cast path writes any of these six slots today, so the scan still degrades to
///         base-slot-9-only in practice -- grounding the indices is inert until a future cast path starts
///         writing one, and can never corrupt an unrelated buff slot since 29-34 sit past every other named
///         slot this codebase currently assigns.
///     </para>
/// </remarks>
public static class HolyShieldResolver
{
    /// <summary>
    ///     Base Holy-Shield BUFF_INFO slot -- skill 82, <see cref="Skills.SkillEffectCatalog" /> slot 9
    ///     (<c>ShieldLifeUp</c>).
    /// </summary>
    public const int BaseSlot = 9;

    /// <summary>
    ///     The six tiered MG5ORIGIN Holy-Shield slots, scanned (in this exact order) ahead of
    ///     <see cref="BaseSlot" /> -- B10 grounds these to the effect-slot tail per <c>DEFINE.h:331-336</c>'s
    ///     35-slot effect-slot maximum (loop 29-34). Safe to populate: no Fenrir cast path writes any of these
    ///     six slots yet, so the tiered scan degrades to the base-slot-9-only fallback exactly as before until a
    ///     future cast path starts writing one -- see this class's own remarks.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:2689-2749 + Server/Header/Protocol/DEFINE.h:331-336.</remarks>
    public static readonly int[] TieredSlots = [29, 30, 31, 32, 33, 34];

    /// <summary>
    ///     Absorbs up to the active shield's remaining value from <paramref name="incomingDamage" /> (the
    ///     pre-element main damage), decrementing that shield in place and clearing it fully if it reaches zero.
    ///     Returns the absorbed amount (0 when no shield is active), so the caller reduces the passed-onward
    ///     damage by exactly that much. Matches <c>DecreaseHolyShield</c>'s "smaller of incoming damage and shield
    ///     value" rule (<c>S07_MyGame04.cpp:2689-2738</c>).
    /// </summary>
    public static int Absorb(int[] buff, int incomingDamage)
    {
        if (incomingDamage <= 0)
            return 0;

        var slot = ResolveActiveSlot(buff);
        if (slot < 0)
            return 0;

        var value = buff[slot * 2];
        var absorbed = Math.Min(incomingDamage, value);
        var remaining = value - absorbed;
        buff[slot * 2] = remaining;
        if (remaining <= 0)
            buff[slot * 2 + 1] = 0; // fully consumed -> clear the duration too (F: "fully cleared")
        return absorbed;
    }

    /// <summary>
    ///     Hard-clears the base and every tiered shield slot (value + duration) -- <c>RemoveHolyShield</c>
    ///     (<c>S07_MyGame04.cpp:2740-2749</c>). Returns true when at least one slot actually changed, so the
    ///     caller only broadcasts on a real change.
    /// </summary>
    public static bool RemoveAll(int[] buff)
    {
        var any = ClearSlot(buff, BaseSlot);
        foreach (var slot in TieredSlots)
            any |= ClearSlot(buff, slot);
        return any;
    }

    /// <summary>First tiered slot with a positive value, else the base slot if positive, else -1 (no shield).</summary>
    private static int ResolveActiveSlot(int[] buff)
    {
        foreach (var slot in TieredSlots)
            if (buff[slot * 2] > 0)
                return slot;

        return buff[BaseSlot * 2] > 0 ? BaseSlot : -1;
    }

    private static bool ClearSlot(int[] buff, int slot)
    {
        if (buff[slot * 2] == 0 && buff[slot * 2 + 1] == 0)
            return false;

        buff[slot * 2] = 0;
        buff[slot * 2 + 1] = 0;
        return true;
    }
}
