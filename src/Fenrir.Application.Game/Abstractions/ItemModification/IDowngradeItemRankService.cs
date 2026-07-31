using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum DowngradeItemRankOutcome
{
    Rejected,
    NoCandidate,
    Applied
}

public readonly record struct DowngradeItemRankResult(
    DowngradeItemRankOutcome Outcome,
    bool Succeeded,
    int Cost,
    int[] Value);

public interface IDowngradeItemRankService
{
    public ValueTask<DowngradeItemRankResult> DowngradeAsync(DowngradeItemRankRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
