using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface IPetActionUpdateService
{
    public void Apply(Zone zone, int characterId, in ActionInfo action);
}
