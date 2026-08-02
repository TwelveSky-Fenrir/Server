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

    /// <summary>
    ///     Wing-enchant destroy-protection charges (<c>aProtectForWing</c>). When > 0, a wing-enchant
    ///     failure that lands in the destroy-risk band consumes one charge (and degrades the item by one
    ///     level, same as an ordinary failure) instead of destroying it outright.
    ///     Ref: Server/ts25zone/S04_MyWork02.cpp:3232-3244.
    ///     <para>
    ///         Persistence: not yet added to <c>CharacterProgressTvp</c>/<c>CharacterWorldSnapshotDto</c> or
    ///         their underlying stored procedures, and no C# item-use path grants a charge yet (legacy grants
    ///         one by consuming item 1237/8437, Server/ts25zone/S04_MyWork03.cpp:3475-3484). In-session only
    ///         until that plumbing lands; coordinate with fenrir-database-engineer before relying on it
    ///         surviving a zone transfer or relog.
    ///     </para>
    /// </summary>
    public int ProtectForWing { get; set; }

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
