namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy <c>mNoMana</c> -- the persistent per-character "consecutive insufficient-mana bot-buff tick"
    ///     counter used only by the zone-126-type-server no-mana escalation
    ///     (<see cref="Simulation.AutoHuntTickSystem" />, <c>Server/ts25zone/S07_MyGame04.cpp:2469-2485</c>):
    ///     incremented once each tick a selected auto-buff's mana cost exceeds current mana while auto-hunt is
    ///     active on a zone-126-type server; at exactly <c>1000</c> the character is relocated to the auto zone,
    ///     beyond <c>1000</c> the session is disconnected. Reset to <c>0</c> whenever an auto-buff cast instead
    ///     proceeds (<c>:2485</c>) and, by construction, on every fresh world entry/zone transfer (Fenrir builds a
    ///     new <see cref="PlayerRuntimeState" /> each time, matching legacy's own zero-init at
    ///     <c>:62</c>).
    /// </summary>
    /// <remarks>
    ///     Legacy also resets this counter from a second, distinct site: a full active-buff-effect wipe routine
    ///     (<c>Server/ts25zone/S07_MyGame04.cpp:1591</c>, the reset itself; <c>:1566-1583</c>, its caller) reached
    ///     from the death-processing path (<c>:1656</c>) and from two weapon-unequip call sites specifically when
    ///     the weapon equipment slot is unequipped or replaced (<c>Server/ts25zone/S04_MyWork03.cpp:2438</c>,
    ///     <c>Server/ts25zone/S04_MyWork05.cpp:1620</c>) -- NOT the same thing as the per-tick, per-slot expiry
    ///     countdown modeled by <see cref="Simulation.BuffExpirySystem" /> (which clears one expired slot at a
    ///     time on its own duration reaching zero, never all slots at once). Fenrir has no equivalent "clear every
    ///     active buff slot at once" routine yet at all -- neither on the death-processing path (the
    ///     <see cref="World.Zone" /> combat/death code) nor on either weapon-unequip call site -- so this
    ///     specific reset path is a genuine, currently-unimplemented gap, not merely an unwired hook on an
    ///     existing system. Documented rather than guessed at here because implementing the wipe routine itself is
    ///     a materially larger, cross-cutting feature (full buff-slot clear on death/weapon-unequip) outside this
    ///     workstream's Simulation-area scope; low practical impact today since the counter only ever grows on the
    ///     rarely-configured zone-126-type shard and a single proceeding auto-buff cast already resets it. A
    ///     follow-up implementing the full wipe routine (likely alongside death-processing/equipment-service work)
    ///     must also reset <see cref="NoManaCount" /> as part of that same wipe, matching legacy's ordering.
    /// </remarks>
    public int NoManaCount { get; set; }

    /// <summary>
    ///     Day-tier of the two-tier paid auto-hunt-time budget (<c>Server/ts25zone/S07_MyGame04.cpp:787-824</c>,
    ///     compiled live under <c>#ifndef FREE_HUNT</c>): a calendar-expiry timestamp in the legacy raw
    ///     <c>YYYYMMDD</c> encoding (<see cref="Simulation.GameDate" />). <c>0</c> = no day-tier budget. Once the
    ///     current date passes this value the day-tier is zeroed; the day-tier takes precedence over
    ///     <see cref="AutoHuntPaidMinuteBudget" /> (the minute-tier is only consumed once this is exhausted).
    /// </summary>
    /// <remarks>
    ///     "Real but currently unreachable", the same posture as <see cref="AutoBuffTime" />/<see cref="AnimalTime" />:
    ///     no acquisition path grants paid auto-hunt time in Fenrir yet (the cash-shop/subscription grant family is
    ///     unimplemented), so this stays <c>0</c> and the budget logic in <see cref="Simulation.AutoHuntTickSystem" />
    ///     is dormant by construction. No persisted <c>game.Characters</c> column backs it yet either, so it resets
    ///     to <c>0</c> on a fresh world entry (a follow-up that adds a paid-time grant path must also carry it
    ///     through <see cref="ZoneTransfer.CreateEnterData" /> and persist it, exactly as
    ///     <see cref="PetExpX2Time" /> documents for its own not-yet-persisted counter).
    /// </remarks>
    public int AutoHuntPaidDayBudget { get; set; }

    /// <summary>
    ///     Minute-tier of the two-tier paid auto-hunt-time budget (<c>S07_MyGame04.cpp:798-810</c>): a consumable
    ///     count of remaining minutes, decremented by one per elapsed real-world minute (floored at zero) only
    ///     once <see cref="AutoHuntPaidDayBudget" /> is exhausted. <c>0</c> = no minute-tier budget. When both
    ///     tiers reach zero the character is relocated to the auto zone (<c>:811-822</c>). Same "real but
    ///     currently unreachable" posture as <see cref="AutoHuntPaidDayBudget" />.
    /// </summary>
    public int AutoHuntPaidMinuteBudget { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator toward the next once-per-real-minute <see cref="AutoHuntPaidMinuteBudget" />
    ///     decrement -- the same per-minute cadence (<c>120</c> legacy ticks =
    ///     <see cref="Simulation.SimulationClock.PlayTimeAccrualLegacyTicks" />) and own-dedicated-accumulator
    ///     pattern <see cref="PetExpX2TimeAccrualTicks" />/<see cref="PlayTimeAccrualTicks" /> already use, kept
    ///     separate for the identical "sharing one accumulator across independently-owned per-minute systems
    ///     would double-consume/desync them" reason those siblings document. Meaningless while both budget tiers
    ///     are zero.
    /// </summary>
    public int AutoHuntBudgetMinuteAccrualTicks { get; set; }
}
