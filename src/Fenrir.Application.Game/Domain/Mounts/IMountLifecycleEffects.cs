using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Mounts;

public interface IMountLifecycleEffects
{
    void ApplyExperience(Zone zone, PlayerRuntimeState state, int garageSlot,
        in MountExperienceCreditResult credit);
}
