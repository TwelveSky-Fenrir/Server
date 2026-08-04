using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmBasicCommandService
{
    public ValueTask HandleVisibilityAsync(int sort, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleSelfTeleportAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleMoveToPositionAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleForceKillMonsterAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleTribeChangeAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleSelfSpecialStateAsync(int sort, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleFindAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    public ValueTask HandleCallAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleMoveToTargetAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleTargetSpecialStateAsync(int sort, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleKickAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleTribeBankAsync(byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken);

    public ValueTask HandleLevelSetAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone,
        CancellationToken cancellationToken);

    public ValueTask HandleStatEditAsync(byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken);
}
