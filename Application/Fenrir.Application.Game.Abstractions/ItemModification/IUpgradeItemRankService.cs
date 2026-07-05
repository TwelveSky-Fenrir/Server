using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum UpgradeItemRankOutcome
{
    Rejected,
    NoCandidate,
    Applied
}

public readonly record struct UpgradeItemRankResult(
    UpgradeItemRankOutcome Outcome,
    bool Succeeded,
    int Cost,
    int[] Value);

public interface IUpgradeItemRankService
{
    public ValueTask<UpgradeItemRankResult> UpgradeAsync(UpgradeItemRankRequest packet, Zone zone,
        PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
