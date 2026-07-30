using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum CraftPetOutcome
{
    Rejected,
    Applied
}

public readonly record struct CraftPetResult(
    CraftPetOutcome Outcome,
    int WireResult,
    int ResultItemId,
    int ResultQuantity,
    int Serial);

public interface ICraftPetService
{
    public ValueTask<CraftPetResult> ResolveFourSlotRecipeAsync(CraftPetRequest packet, Zone zone,
        PlayerRuntimeState state,
        int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<CraftPetResult> ResolveTwoSlotRecipeAsync(CraftPetRequest packet, Zone zone,
        PlayerRuntimeState state,
        int characterId, int accountId, CancellationToken cancellationToken);
}
