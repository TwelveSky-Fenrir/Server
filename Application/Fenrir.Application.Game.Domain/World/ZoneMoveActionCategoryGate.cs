namespace Fenrir.Application.Game.Domain.World;

public enum ZoneMoveActionCategory
{
    GmMove = 2,
    Death = 3,
    Portal = 4,
    NpcMoneyGated = 5,
    NpcMove = 6,
    Return = 7,
    ReturnItem = 8,
    MoveItem = 9,
    NpcTeleporter = 10,
    AutoToZone037 = 11,
    Zone84Transition = 12
}

public static class ZoneMoveActionCategoryGate
{
    public static bool IsRecognized(int sort) => sort is
        (int)ZoneMoveActionCategory.GmMove or
        (int)ZoneMoveActionCategory.Death or
        (int)ZoneMoveActionCategory.Portal or
        (int)ZoneMoveActionCategory.NpcMoneyGated or
        (int)ZoneMoveActionCategory.NpcMove or
        (int)ZoneMoveActionCategory.Return or
        (int)ZoneMoveActionCategory.ReturnItem or
        (int)ZoneMoveActionCategory.MoveItem or
        (int)ZoneMoveActionCategory.NpcTeleporter or
        (int)ZoneMoveActionCategory.AutoToZone037 or
        (int)ZoneMoveActionCategory.Zone84Transition;
}
