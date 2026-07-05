using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Progression;

public enum HeroRewardClaimOutcome
{
    /// <summary>Caller is not present in the Previous-period standings for their live tribe -- no reply at all.</summary>
    NotRanked,

    /// <summary>Ranked, but the reward row is already marked claimed -- Result = 3.</summary>
    AlreadyClaimed,

    /// <summary>Reward granted this call -- Result = 1000.</summary>
    Claimed
}

public readonly record struct HeroRewardClaimResult(HeroRewardClaimOutcome Outcome);

public interface IHeroRewardClaimService
{
    public ValueTask<HeroRewardClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
