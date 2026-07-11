using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Guilds;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Guilds;

public class GuildActionServiceDisbandTests
{
    private const int CharacterId = 1;
    private const int GuildId = 10;

    [Fact]
    public async Task DisbandGuild_SoleRemainingMaster_CallsDisbandAsync_WithTheActingCharacterId()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", GuildId, 2);
        guilds.SeedRoster(GuildId,
            new GuildRosterRowDto(GuildId, "Aesir", CharacterId, "Odin", 2, "", DateTime.UtcNow));

        var service = CreateService(zones, guilds);

        var resultTask =
            service.DisbandGuildAsync(zones[1], state, CharacterId, CancellationToken.None).AsTask();
        zones[1].Tick(TimeSpan.FromMilliseconds(50));
        var result = await resultTask;

        Assert.False(result.Abort);
        Assert.Equal(6, result.Sort);
        Assert.Equal(0, result.Result);

        Assert.NotNull(guilds.LastDisband);
        var (disbandedGuildId, actingCharacterId) = guilds.LastDisband!.Value;
        Assert.Equal(GuildId, disbandedGuildId);
        Assert.Equal(CharacterId, actingCharacterId);
    }

    [Fact]
    public async Task DisbandGuild_MoreThanOneMemberRemaining_RefusesWithoutCallingDisbandAsync()
    {
        const int otherMemberId = 2;
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", GuildId, 2);
        guilds.SeedRoster(GuildId,
            new GuildRosterRowDto(GuildId, "Aesir", CharacterId, "Odin", 2, "", DateTime.UtcNow),
            new GuildRosterRowDto(GuildId, "Aesir", otherMemberId, "Thor", 0, "", DateTime.UtcNow));

        var service = CreateService(zones, guilds);

        var result = await service.DisbandGuildAsync(zones[1], state, CharacterId, CancellationToken.None);

        Assert.False(result.Abort);
        Assert.Equal(6, result.Sort);
        Assert.Equal(2, result.Result);
        Assert.Null(guilds.LastDisband);
    }

    [Fact]
    public async Task DisbandGuild_NotMaster_AbortsWithoutCallingDisbandAsync()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", GuildId, 0);
        guilds.SeedRoster(GuildId,
            new GuildRosterRowDto(GuildId, "Aesir", CharacterId, "Odin", 0, "", DateTime.UtcNow));

        var service = CreateService(zones, guilds);

        var result = await service.DisbandGuildAsync(zones[1], state, CharacterId, CancellationToken.None);

        Assert.True(result.Abort);
        Assert.Null(guilds.LastDisband);
    }

    [Fact]
    public async Task DisbandGuild_NoGuild_AbortsWithoutCallingDisbandAsync()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", null, 0);

        var service = CreateService(zones, guilds);

        var result = await service.DisbandGuildAsync(zones[1], state, CharacterId, CancellationToken.None);

        Assert.True(result.Abort);
        Assert.Null(guilds.LastDisband);
    }

    private static GuildActionService CreateService(ZoneRegistry zones, FakeGuildRepository guilds)
    {
        return new GuildActionService(zones, guilds, new GuildInviteRegistry(),
            NullLogger<GuildActionService>.Instance);
    }

    private static (ZoneRegistry Zones, FakeGuildRepository Guilds) CreateWorld()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1]);
        return (registry, new FakeGuildRepository());
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) EnterZone(
        ZoneRegistry zones, short mapId, int characterId, string name, int? guildId, byte guildRoleDb)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        var zone = zones[mapId];
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, name)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        zone.TryGetPlayer(characterId, out var state);
        state!.GuildId = guildId;
        state.GuildRoleDb = guildRoleDb;
        state.GuildName = guildId is null ? "" : "Aesir";

        return (session, pipe, state);
    }
}
