using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers.Tribes;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeBankServiceTests
{
    private const int CharacterId = 42;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        byte tribe, byte tribeRole)
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

        return (session, pipe, state);
    }

    [Fact]
    public async Task View_WithNoTribeRole_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 0);
        var service = new TribeBankService(new FakeTribeRepository(), NullLogger<TribeBankService>.Instance);

        var result = await service.ViewAsync(state, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task View_WithTribeRole_ReturnsBankArray()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 2, 2);
        var repository = new FakeTribeRepository();
        repository.Bank[(2, 5)] = 7_000;
        repository.Bank[(2, 49)] = 12;
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.ViewAsync(state, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Sort);
        Assert.NotNull(result.TribeBankInfo);
        Assert.Equal(7_000, result.TribeBankInfo![5]);
        Assert.Equal(12, result.TribeBankInfo[49]);
        Assert.Equal(0, result.Money);
    }

    [Fact]
    public async Task Withdraw_NotForceLeader_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 2);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.WithdrawAsync(0, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_NotEnoughSubMasters_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.Add(new TribeSubMasterDto(1, 0, 100));
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.WithdrawAsync(0, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_RepositoryThrows_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        repository.WithdrawException = new InvalidOperationException("insufficient tribe bank balance");
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.WithdrawAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Withdraw_Success_ReturnsUpdatedBankAndMoney()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        repository.Bank[(1, 4)] = 50_000;
        repository.MoneyAfterWithdraw = 999_999;
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.WithdrawAsync(4, state, CharacterId, CancellationToken.None);

        Assert.Equal(((byte)1, (byte)4, CharacterId), repository.LastWithdrawCall);
        Assert.True(result.Success);
        Assert.Equal(2, result.Sort);
        Assert.NotNull(result.TribeBankInfo);
        Assert.Equal(0, result.TribeBankInfo![4]);
        Assert.Equal(999_999, result.Money);
    }

    [Fact]
    public async Task UnknownSort_Aborts()
    {
        // Sort 1/2/3 dispatch to View/Withdraw/DepositAsync respectively; any other sort is a handler-owned
        // fallback the service itself never sees -- exercise the real handler here.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 1);
        var service = new TribeBankService(new FakeTribeRepository(), NullLogger<TribeBankService>.Instance);
        var handler = new TribeBankHandler(service);

        await handler.HandleAsync(new TribeBankRequest { Sort = 4, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Deposit_SlotIndexOutOfRange_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository();
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(50, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastDepositCall);
    }

    [Fact]
    public async Task Deposit_RegularMemberWithNoTribeRole_StillAllowed()
    {
        // Unlike view/withdraw, deposit only ever moves the depositor's own money -- no privileged role
        // (TribeRole == 0, i.e. not master/sub-master/vote-candidate) is required.
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository { MoneyAfterDeposit = 0, DepositAmount = 5_000 };
        repository.Bank[(1, 4)] = 0;
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(4, state, CharacterId, CancellationToken.None);

        Assert.Equal(((byte)1, (byte)4, CharacterId), repository.LastDepositCall);
        Assert.True(result.Success);
        Assert.Equal(3, result.Sort);
    }

    [Fact]
    public async Task Deposit_RepositoryThrows_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository
        {
            DepositException = new InvalidOperationException("character has no money to deposit")
        };
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Deposit_Success_ReturnsUpdatedBankAndMoney()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository { MoneyAfterDeposit = 0, DepositAmount = 30_000 };
        repository.Bank[(1, 9)] = 20_000;
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(9, state, CharacterId, CancellationToken.None);

        Assert.Equal(((byte)1, (byte)9, CharacterId), repository.LastDepositCall);
        Assert.True(result.Success);
        Assert.NotNull(result.TribeBankInfo);
        Assert.Equal(50_000, result.TribeBankInfo![9]);
        Assert.Equal(0, result.Money);
    }
}
