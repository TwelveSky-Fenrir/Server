using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum CraftSkillBookOutcome
{
    Rejected,
    Applied
}

public readonly record struct CraftSkillBookResult(CraftSkillBookOutcome Outcome, int ResultItemId, int Serial);

public interface ICraftSkillBookService
{
    public ValueTask<CraftSkillBookResult> ResolveAsync(CraftSkillBookRequest packet, Zone zone,
        PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
