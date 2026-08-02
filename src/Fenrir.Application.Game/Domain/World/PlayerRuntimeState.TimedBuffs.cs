namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public int FightingGodForDestroy { get; set; }

    public int DoubleExpTime1 { get; set; }

    public int DoubleExpTime2 { get; set; }


    public int AnimalDoubleExp { get; set; }

    public int DmgBoost { get; set; }

    public int HPBoost { get; set; }

    public int CriBoost { get; set; }

    public int WarriorPill { get; set; }

    public int WarriorScroll { get; set; }

    /// <summary>Minutes remaining on the Scroll of Loyalty / Scroll of the Gods buff (bonus CP per PvP kill).</summary>
    /// <remarks>
    ///     Decremented each minute by <see cref="TimedBuffCountdownSystem"/> in Group B zones.
    ///     When greater than zero, the character receives extra contribution points on each PvP kill.
    ///     Persisted via ProgressWriteBehindHost.
    ///     Réf. legacy field: <c>aDoubleKillNumTime</c> — Server/ts25zone/S04_MyWork03.cpp.
    /// </remarks>
    public int DoubleKillNumTime { get; set; }

    /// <summary>Minutes remaining on the Scroll of Battle / Scroll of the Gods buff (bonus EXP per PvP kill).</summary>
    /// <remarks>
    ///     Decremented each minute by <see cref="TimedBuffCountdownSystem"/> in Group B zones.
    ///     When greater than zero, the character receives an 8× EXP multiplier on each PvP kill
    ///     (same <c>DoubleExpChargeMultiplier</c> path already in <see cref="PvpKillExperienceCalculator"/>).
    ///     Persisted via ProgressWriteBehindHost.
    ///     Réf. legacy field: <c>aDoubleKillExpTime</c> — Server/ts25zone/S04_MyWork03.cpp.
    /// </remarks>
    public int DoubleKillExpTime { get; set; }

    /// <summary>Per-kill charge count for the Crushed Demon Scroll buff.</summary>
    /// <remarks>
    ///     Unlike DoubleKillNumTime/DoubleKillExpTime, this counter is decremented by 1 on each PvP kill
    ///     (NOT per minute) and broadcasts sort 30 (<c>S030</c>) after each decrement.
    ///     Starting value: +50 per scroll use. When the counter reaches zero the buff expires naturally.
    ///     Persisted via ProgressWriteBehindHost.
    ///     Réf. legacy field: <c>aDoubleKillNumTime2</c> — Server/ts25zone/S07_MyGame02.cpp:2445-2448.
    /// </remarks>
    public int DoubleKillNumTime2 { get; set; }

    /// <summary>Minutes remaining on the Silver Ornament scroll bonus.</summary>
    /// <remarks>
    ///     Grants ornament stat bonuses (HP/MP/ATK/DEF) while <see cref="UseOrnament"/> is active and this
    ///     counter is greater than zero. Ticked by <see cref="TimedBuffCountdownSystem"/> in Group B zones.
    ///     Persisted via ProgressWriteBehindHost. Réf. sort: S090.
    /// </remarks>
    public int SilverTime { get; set; }

    /// <summary>Minutes remaining on the Gold Ornament scroll bonus.</summary>
    /// <remarks>
    ///     Grants ornament stat bonuses (HP/MP/ATK/DEF) while <see cref="UseOrnament"/> is active and this
    ///     counter is greater than zero. Ticked by <see cref="TimedBuffCountdownSystem"/> in Group B zones.
    ///     Persisted via ProgressWriteBehindHost. Réf. sort: S101.
    /// </remarks>
    public int GoldTime { get; set; }


    public int Zone101Time { get; set; }

    public int Zone126Time { get; set; }

    public int Zone050Time2 { get; set; }


    public int UserSort { get; set; }

    public int TimedBuffCountdownAccrualTicks { get; set; }

    public bool PaidZoneEvictionPending { get; set; }
}
