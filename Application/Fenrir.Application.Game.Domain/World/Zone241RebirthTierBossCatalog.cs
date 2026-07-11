namespace Fenrir.Application.Game.Domain.World;

public sealed class Zone241RebirthTierBossCatalog : IPersonalDungeonBossCatalog
{
    public static readonly Zone241RebirthTierBossCatalog Instance = new();

    private Zone241RebirthTierBossCatalog()
    {
    }

    public bool TryGetBossMonsterId(int rebirthTier, out int monsterId)
    {
        monsterId = PersonalDungeonBossTables.ResolveCatalogE(rebirthTier);
        return true;
    }
}
