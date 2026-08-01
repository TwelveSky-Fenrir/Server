using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Progression;

public enum HeroRewardClaimOutcome
{
    NotRanked,

    AlreadyClaimed,

    Claimed
}

public readonly record struct HeroRewardClaimResult(HeroRewardClaimOutcome Outcome);

public interface IHeroRewardClaimService
{
    public ValueTask<HeroRewardClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
