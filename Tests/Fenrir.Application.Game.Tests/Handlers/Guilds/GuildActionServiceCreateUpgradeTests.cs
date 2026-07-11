using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Guilds;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Guilds;

public class GuildActionServiceCreateUpgradeTests
{
    private const int CharacterId = 1;
    private const int GuildId = 10;

    [Fact]
    public async Task CreateGuild_Success_CallsTheCombinedProcedureOnce_AndReturnsTheNewGuildInfo()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", null, 0, 42);
        var service = CreateService(zones, guilds);

        var data = new byte[500];
        new GuildWorkCreatePayload { GuildName = "Aesir" }.Write(data);

        var resultTask = service.CreateGuildAsync(new GuildActionRequest { Sort = 1, Data = data }, zones[1], state,
            CharacterId, CancellationToken.None).AsTask();
        zones[1].Tick(TimeSpan.FromMilliseconds(50));
        var result = await resultTask;

        Assert.False(result.Abort);
        Assert.Equal(1, result.Sort);
        Assert.Equal(0, result.Result);
        Assert.Equal("Aesir", result.GuildInfo.Name);

        Assert.NotNull(guilds.LastCreateAndDebitMoney);
        var (name, masterCharacterId, deltaMoney, deltaBigMoney) = guilds.LastCreateAndDebitMoney!.Value;
        Assert.Equal("Aesir", name);
        Assert.Equal(CharacterId, masterCharacterId);
        Assert.Equal(-10_000_000, deltaMoney);
        Assert.Equal(0, deltaBigMoney);
    }

    [Fact]
    public async Task CreateGuild_CombinedProcedureThrows_ReturnsAGracefulFailure_WithNoCompensatingDisbandCall()
    {
        var (zones, guilds) = CreateWorld();
        guilds.ThrowOnCreateAndDebitMoney = true;
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", null, 0, 42);
        var service = CreateService(zones, guilds);

        var data = new byte[500];
        new GuildWorkCreatePayload { GuildName = "Aesir" }.Write(data);

        var result = await service.CreateGuildAsync(new GuildActionRequest { Sort = 1, Data = data }, zones[1],
            state, CharacterId, CancellationToken.None);

        Assert.False(result.Abort);
        Assert.Equal(1, result.Sort);
        Assert.Equal(1, result.Result);
        Assert.Null(guilds.LastDisband);
    }

    [Fact]
    public async Task CreateGuild_LevelBelow30_AbortsWithoutCallingTheRepository()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", null, 0, 20);
        var service = CreateService(zones, guilds);

        var data = new byte[500];
        new GuildWorkCreatePayload { GuildName = "Aesir" }.Write(data);

        var result = await service.CreateGuildAsync(new GuildActionRequest { Sort = 1, Data = data }, zones[1],
            state, CharacterId, CancellationToken.None);

        Assert.True(result.Abort);
        Assert.Null(guilds.LastCreateAndDebitMoney);
    }

    [Fact]
    public async Task UpgradeGuild_Success_CallsTheCombinedProcedureOnce_WithTheIncrementedGrade()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", GuildId, 2, 60);
        guilds.Seed(SeedGuild(1, 10));
        guilds.SeedRoster(GuildId, RosterOfSize(10));
        var service = CreateService(zones, guilds);

        var result = await service.UpgradeGuildAsync(state, CharacterId, CancellationToken.None);

        Assert.False(result.Abort);
        Assert.Equal(7, result.Sort);
        Assert.Equal(0, result.Result);

        Assert.NotNull(guilds.LastUpgradeAndDebitMoney);
        var (setGuildId, grade, characterId, deltaMoney, deltaBigMoney) = guilds.LastUpgradeAndDebitMoney!.Value;
        Assert.Equal(GuildId, setGuildId);
        Assert.Equal(2, grade);
        Assert.Equal(CharacterId, characterId);
        Assert.Equal(-20_000_000, deltaMoney);
        Assert.Equal(0, deltaBigMoney);
    }

    [Fact]
    public async Task UpgradeGuild_CombinedProcedureThrows_ReturnsAGracefulFailure_WithNoCompensatingGradeRollback()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", GuildId, 2, 60);
        guilds.Seed(SeedGuild(1, 10));
        guilds.SeedRoster(GuildId, RosterOfSize(10));
        guilds.ThrowOnUpgradeAndDebitMoney = true;
        var service = CreateService(zones, guilds);

        var result = await service.UpgradeGuildAsync(state, CharacterId, CancellationToken.None);

        Assert.False(result.Abort);
        Assert.Equal(7, result.Sort);
        Assert.Equal(1, result.Result);
    }

    [Fact]
    public async Task UpgradeGuild_NotMaster_AbortsWithoutCallingTheRepository()
    {
        var (zones, guilds) = CreateWorld();
        var (_, _, state) = EnterZone(zones, 1, CharacterId, "Odin", GuildId, 0, 60);
        guilds.Seed(SeedGuild(1, 10));
        guilds.SeedRoster(GuildId, RosterOfSize(10));
        var service = CreateService(zones, guilds);

        var result = await service.UpgradeGuildAsync(state, CharacterId, CancellationToken.None);

        Assert.True(result.Abort);
        Assert.Null(guilds.LastUpgradeAndDebitMoney);
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
        ZoneRegistry zones, short mapId, int characterId, string name, int? guildId, byte guildRoleDb, short level)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        var zone = zones[mapId];
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, name, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        zone.TryGetPlayer(characterId, out var state);
        state!.GuildId = guildId;
        state.GuildRoleDb = guildRoleDb;
        state.GuildName = guildId is null ? "" : "Aesir";

        return (session, pipe, state);
    }

    private static GuildSummaryDto SeedGuild(int grade, int memberCount)
    {
        return new GuildSummaryDto(GuildId, "Aesir", grade, CharacterId, 0,
            0, 0, 0, 0, 0, DateTime.UtcNow,
            memberCount);
    }

    private static GuildRosterRowDto[] RosterOfSize(int count)
    {
        var rows = new GuildRosterRowDto[count];
        for (var i = 0; i < count; i++)
            rows[i] = new GuildRosterRowDto(GuildId, "Aesir", CharacterId + i, $"Member{i}",
                i == 0 ? (byte)2 : (byte)0, "", DateTime.UtcNow);
        return rows;
    }
}
