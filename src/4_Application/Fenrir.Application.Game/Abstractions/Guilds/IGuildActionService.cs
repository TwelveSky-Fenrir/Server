using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Guilds;

public readonly record struct GuildActionResult(bool Abort, int Sort, int Result, GuildInfo GuildInfo)
{
    public static readonly GuildActionResult Aborted = new(true, 0, 0, default);

    public static GuildActionResult Success(int sort, GuildInfo info, int result = 0)
    {
        return new GuildActionResult(false, sort, result, info);
    }
}

public interface IGuildActionService
{
    public ValueTask<GuildActionResult> CreateGuildAsync(GuildActionRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct);

    public ValueTask<GuildActionResult> GetGuildInfoAsync(PlayerRuntimeState state, CancellationToken ct);

    public ValueTask<GuildActionResult> FinalizeInviteAsync(PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<GuildActionResult> ExitGuildAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<GuildActionResult> UpdateGuildNoticeAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    public ValueTask<GuildActionResult> DisbandGuildAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<GuildActionResult> UpgradeGuildAsync(PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<GuildActionResult> KickMemberAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    public ValueTask<GuildActionResult> SetAgmRoleAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    public ValueTask<GuildActionResult> SetMemberTitleAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    public ValueTask<GuildActionResult> SetGuildBuffAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);

    public ValueTask<GuildActionResult> TransferLeadershipAsync(GuildActionRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken ct);

    public ValueTask<GuildActionResult> SetGuildLogoAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct);
}
