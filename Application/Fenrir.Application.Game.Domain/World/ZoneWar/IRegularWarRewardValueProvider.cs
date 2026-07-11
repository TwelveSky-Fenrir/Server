namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public interface IRegularWarRewardValueProvider
{
    public long GetMoneyReward(short rebirthTier);

    public int GetExperienceReward(short level);
}

public sealed class UnavailableRegularWarRewardValueProvider : IRegularWarRewardValueProvider
{
    public long GetMoneyReward(short rebirthTier)
    {
        return 0;
    }

    public int GetExperienceReward(short level)
    {
        return 0;
    }
}
