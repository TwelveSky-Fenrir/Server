using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.World.Npcs;

/// <summary>
///     Ports <c>ZONENPCINFO::CheckNPCFunction</c> (<c>Server/ts25zone/S07_MyGame07.cpp:230-257</c>): true when
///     ANY NPC placed in this zone, within <see cref="ProximityRadius" /> of the player, advertises this
///     numbered function (<c>nMenu[functionId] == 2</c>) -- not "is the player next to the specific NPC it named".
/// </summary>
public static class NpcFunctionGate
{
    /// <summary><c>GetDoubleXYZ(...) &lt; 10000.0f</c> is a squared-distance compare; real radius is sqrt(10000) = 100.</summary>
    public const float ProximityRadius = 100f;

    private const float ProximityRadiusSquared = ProximityRadius * ProximityRadius;

    /// <summary>Function id 1 -- <c>ProcessForLearnSkill1</c> (tSort 202), skill tree 1.</summary>
    public const int LearnSkillTree1 = 1;

    /// <summary>
    ///     Function id 4 -- NPC shop buy/sell (tSort 212/215/252, <c>ProcessForInventoryToNPCShop</c>/
    ///     <c>ProcessForNPCShopToInventory</c>).
    /// </summary>
    public const int NpcShop = 4;

    /// <summary>Function id 37 -- <c>ProcessForLearnSkill2</c> (tSort 233), tribe-4 skill tree 2.</summary>
    public const int LearnSkillTree2 = 37;

    /// <summary>
    ///     True when at least one NPC in <paramref name="zone" /> advertises <paramref name="functionId" /> and is
    ///     within <see cref="ProximityRadius" /> of the player. <paramref name="functionId" /> outside [0,100]
    ///     always returns false, matching the legacy's own bound.
    /// </summary>
    public static bool IsAvailable(ZoneDefinition zone, WorldDataCache worldData, int functionId, float posX,
        float posY, float posZ)
    {
        if (functionId is < 0 or > 100)
            return false;

        foreach (var spawn in zone.NpcSpawns)
        {
            // NpcSpawns is pre-filtered at cache-build time -- NpcId is never null here.
            if (!worldData.NpcsById.TryGetValue(spawn.NpcId!.Value, out var npc))
                continue;

            var offersFunction = false;
            foreach (var option in npc.MenuOptions)
                if (option.SlotIndex == functionId && option.OptionId == 2)
                {
                    offersFunction = true;
                    break;
                }

            if (!offersFunction)
                continue;

            var dx = spawn.PosX - posX;
            var dy = spawn.PosY - posY;
            var dz = spawn.PosZ - posZ;
            if (dx * dx + dy * dy + dz * dz < ProximityRadiusSquared)
                return true;
        }

        return false;
    }
}
