namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public interface IRegularWarRewardValueProvider
{
    public long GetMoneyReward(short evolutionTier, short level);

    public int GetExperienceReward(short level);
}

public sealed class UnavailableRegularWarRewardValueProvider : IRegularWarRewardValueProvider
{
    public long GetMoneyReward(short evolutionTier, short level)
    {
        return 0;
    }

    public int GetExperienceReward(short level)
    {
        return 0;
    }
}
