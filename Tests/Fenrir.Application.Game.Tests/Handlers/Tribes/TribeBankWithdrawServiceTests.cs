using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Tribes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeBankWithdrawServiceTests
{
    private const int CharacterId = 42;

    private static PlayerRuntimeState Setup(Zone zone, byte tribe, byte tribeRole)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        zone.TryGetPlayer(CharacterId, out var state);
        state!.TribeRole = tribeRole;
        return state;
    }

    private static FakeTribeRepository WithQuorum(byte tribe)
    {
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(tribe, 0, 100),
            new TribeSubMasterDto(tribe, 1, 101),
            new TribeSubMasterDto(tribe, 2, 102)
        ]);
        return repository;
    }

    [Fact]
    public async Task Withdraw_NotForceLeader_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = Setup(zone, 1, 2);
        var repository = WithQuorum(1);
        var service = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);

        var result = await service.WithdrawAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_SlotOutOfRange_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = Setup(zone, 1, 1);
        var repository = WithQuorum(1);
        var service = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);

        var result = await service.WithdrawAsync(50, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_FewerThanThreeSubMasters_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.Add(new TribeSubMasterDto(1, 0, 100));
        var service = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);

        var result = await service.WithdrawAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_ProcedureThrows_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = Setup(zone, 1, 1);
        var repository = WithQuorum(1);
        repository.WithdrawException = new InvalidOperationException("insufficient tribe bank balance");
        var service = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);

        var result = await service.WithdrawAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(((byte)1, (byte)4, CharacterId), repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_Success_ReturnsSort2_NewMoney_AndTheDrainedBankArray()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = Setup(zone, 1, 1);
        var repository = WithQuorum(1);
        repository.Bank[(1, 9)] = 20_000;
        repository.MoneyAfterWithdraw = 20_000;

        var service = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);

        var result = await service.WithdrawAsync(9, state, CharacterId, CancellationToken.None);

        Assert.Equal(((byte)1, (byte)9, CharacterId), repository.LastWithdrawCall);
        Assert.True(result.Success);
        Assert.Equal(2, result.Sort);
        Assert.Equal(20_000, result.Money);
        Assert.NotNull(result.TribeBankInfo);
        Assert.Equal(0, result.TribeBankInfo![9]);
    }
}
