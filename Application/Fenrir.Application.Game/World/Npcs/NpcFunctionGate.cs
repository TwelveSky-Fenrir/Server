using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.World.Npcs;

/// <summary>
///     Ports <c>ZONENPCINFO::CheckNPCFunction</c> EXACTLY (verified against source,
///     <c>Server/ts25zone/S07_MyGame07.cpp:230-257</c>) -- the proximity gate report 04_mega_switches.md §1
///     shows guarding NPC-menu-driven tSort actions (202 function 1, 212/215/252 function 4, 233 function
///     37...) BEFORE the caller does its own per-NPC data validation (e.g. <c>ProcessForLearnSkill1</c>
///     re-searching that SAME NPC's own skill offers by the client-supplied NpcId). This is NOT "is the
///     player standing next to the specific NPC it named" -- it is "does ANY NPC placed in THIS zone, within
///     <see cref="ProximityRadius" /> legacy units of the player, advertise this numbered function at all"
///     (<c>nMenu[functionId] == 2</c>). Pure/Zone-independent: no I/O, unit-testable.
/// </summary>
public static class NpcFunctionGate
{
    /// <summary>
    ///     <c>GetDoubleXYZ(...) &lt; 10000.0f</c> (mapcheck.h) is a SQUARED-distance compare -- the real radius
    ///     is sqrt(10000) = 100 legacy units, not 10000 itself.
    /// </summary>
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
    ///     True when at least one NPC placed in <paramref name="zone" /> advertises
    ///     <paramref name="functionId" /> (<c>nMenu[functionId] == 2</c>, world.NpcMenuOptions) AND is within
    ///     <see cref="ProximityRadius" /> legacy units of (<paramref name="posX" />, <paramref name="posY" />,
    ///     <paramref name="posZ" />). <paramref name="functionId" /> outside [0,100] (the legacy's own
    ///     <c>nMenu[100]</c> bound) always returns false, matching <c>CheckNPCFunction</c>'s own guard.
    /// </summary>
    public static bool IsAvailable(ZoneDefinition zone, WorldDataCache worldData, int functionId, float posX,
        float posY, float posZ)
    {
        if (functionId is < 0 or > 100)
            return false;

        foreach (var spawn in zone.NpcSpawns)
        {
            // ZoneDefinition.NpcSpawns is pre-filtered at cache-build time: NpcId is never null here (see
            // WorldDataCacheBuilder.BuildZones's own remarks) -- .Value is safe, not a defensive guess.
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
