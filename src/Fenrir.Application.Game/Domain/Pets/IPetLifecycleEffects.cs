using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Pets;

public interface IPetLifecycleEffects
{
    void Apply(Zone zone, PlayerRuntimeState state, in PetLifecycleTransition transition);
}
