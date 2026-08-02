namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Death-protection shield stacks (<c>aProtectForDeath</c>). When > 0 the shield absorbs one death
    ///     event (EXP loss or CP loss at level cap) and is decremented instead of deducting EXP/CP.
    ///     Ref: Server/ts25zone/S07_MyGame02.cpp:2921-2964.
    ///     <para>
    ///         Persistence: requires <c>ProtectForDeath</c> to be added to <c>CharacterProgressTvp</c> and
    ///         the underlying stored procedure before decrements survive zone transitions.
    ///         Coordinate with <b>fenrir-database-engineer</b> before shipping shield-active gameplay.
    ///     </para>
    /// </summary>
    public int ProtectForDeath { get; set; }

    public int LodRounds { get; set; }

    public int ProtectForRefine { get; set; }

    public int ProtectForDestroy { get; set; }

    public int ProtectForCostume { get; set; }

    public int ProtectForDestroy2 { get; set; }

    public int ImproveItemValue { get; set; }

    public int AddItemValue { get; set; }

    public int HighItemValue { get; set; }

    public int DropItemTime { get; set; }

    public int TaiyanKeyTimer { get; set; }

    public int EatLifePotion { get; set; }

    public int EatManaPotion { get; set; }

    public int EatStrPotion { get; set; }

    public int EatDexPotion { get; set; }

    public int EatElePotion { get; set; }
}
