using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmBasicCommandService
{
    public ValueTask HandleVisibilityAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleSelfTeleportAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleForceKillMonsterAsync(byte[] data, ZoneClientSession zoneSession, Zone zone,
        CancellationToken cancellationToken);

    public ValueTask HandleTribeChangeAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleSelfSpecialStateAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleFindAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    public ValueTask HandleCallAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    public ValueTask HandleMoveToTargetAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    public ValueTask HandleTargetSpecialStateAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, CancellationToken cancellationToken);

    public ValueTask HandleKickAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    public ValueTask HandleTribeBankAsync(byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);

    public ValueTask HandleLevelSetAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone,
        CancellationToken cancellationToken);

    public ValueTask HandleStatEditAsync(byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
