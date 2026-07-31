using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface IPetActionUpdateService
{
    public void Apply(Zone zone, int characterId, in ActionInfo action);
}
