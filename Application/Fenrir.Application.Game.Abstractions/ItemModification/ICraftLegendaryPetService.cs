using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum CraftLegendaryPetOutcome
{
    Rejected,
    Applied
}

public readonly record struct CraftLegendaryPetResult(CraftLegendaryPetOutcome Outcome, int ResultItemId, int Serial);

public interface ICraftLegendaryPetService
{
    public ValueTask<CraftLegendaryPetResult> ResolveAsync(CraftLegendaryPetRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
