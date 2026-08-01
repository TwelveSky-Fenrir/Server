using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public long PremiumExpireUtc { get; set; }

    public int BuffX2Time { get; set; }

    public int SupportSkillTimeUpRatio { get; set; } = 1;

    public int SupportSkillTimeUpRatioAccrualTicks { get; set; }

    public void RecomputeSupportSkillTimeUpRatio()
    {
        var buffDurationExtensionActive = BuffX2Time > 0;
        var premiumActive = PremiumExpireUtc != 0;
        SupportSkillTimeUpRatio =
            SupportSkillTimeUpRatioCalculator.Compute(buffDurationExtensionActive, premiumActive);
    }
}
