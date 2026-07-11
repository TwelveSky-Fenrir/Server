namespace Fenrir.Application.Game.Domain.World;

public interface IPersonalDungeonBossCatalog
{
    public bool TryGetBossMonsterId(int rebirthTier, out int monsterId);
}

public sealed class NullPersonalDungeonBossCatalog : IPersonalDungeonBossCatalog
{
    public static readonly NullPersonalDungeonBossCatalog Instance = new();

    private NullPersonalDungeonBossCatalog()
    {
    }

    public bool TryGetBossMonsterId(int rebirthTier, out int monsterId)
    {
        monsterId = 0;
        return false;
    }
}
