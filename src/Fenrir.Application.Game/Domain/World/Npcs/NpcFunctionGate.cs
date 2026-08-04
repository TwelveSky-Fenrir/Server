using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Npcs;

public enum NpcProximity
{
    NpcNotInZone,

    Near,

    Far
}

public static class NpcFunctionGate
{
    public const float ProximityRadius = 100f;

    private const float ProximityRadiusSquared = ProximityRadius * ProximityRadius;

    public const int LearnSkillTree1 = 1;

    public const int NpcShop = 4;

    public const int LearnSkillTree2 = 37;

    public static bool IsAvailable(ZoneDefinition zone, WorldDataCache worldData, int functionId, float posX,
        float posY, float posZ)
    {
        if (functionId is < 0 or > 100)
            return false;

        foreach (var spawn in zone.NpcSpawns)
        {
            if (spawn.NpcId is not int npcId || !worldData.NpcsById.TryGetValue(npcId, out var npc))
                continue;

            if (!OffersFunction(npc, functionId))
                continue;

            var dx = spawn.PosX - posX;
            var dy = spawn.PosY - posY;
            var dz = spawn.PosZ - posZ;
            if (dx * dx + dy * dy + dz * dz < ProximityRadiusSquared)
                return true;
        }

        return false;
    }

    public static bool IsAvailableAtNpc(ZoneDefinition zone, NpcDefinition npc, int functionId, float posX,
        float posY, float posZ)
    {
        return functionId is >= 0 and <= 100 &&
               OffersFunction(npc, functionId) &&
               CheckNpcProximity(zone, npc.Npc.NpcId, posX, posY, posZ) == NpcProximity.Near;
    }

    public static NpcProximity CheckNpcProximity(ZoneDefinition zone, int npcNumber, float posX, float posY,
        float posZ)
    {
        var placed = false;

        foreach (var spawn in zone.NpcSpawns)
        {
            if (spawn.NpcId != npcNumber)
                continue;

            placed = true;

            var dx = spawn.PosX - posX;
            var dy = spawn.PosY - posY;
            var dz = spawn.PosZ - posZ;
            if (dx * dx + dy * dy + dz * dz < ProximityRadiusSquared)
                return NpcProximity.Near;
        }

        return placed ? NpcProximity.Far : NpcProximity.NpcNotInZone;
    }

    private static bool OffersFunction(NpcDefinition npc, int functionId)
    {
        foreach (var option in npc.MenuOptions)
            if (option.SlotIndex == functionId && option.OptionId == 2)
                return true;

        return false;
    }
}
