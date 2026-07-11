using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Tribes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

// TribeBankWithdrawService is the tribe-bank WITHDRAW / LOAD app caller (C17 Part A sort 2). A fresh,
// definitive full read of Server/ts25zone/S04_MyWork02.cpp:11560-11607 and
// Server/ts25playuser/S04_MyWork02.cpp:269-377 resolved a 3-way source contradiction: CZ_TRIBE_BANK_SEND
// sort 2 is EXCLUSIVELY this withdraw (bank slot -> player money) -- legacy has no client-invocable deposit
// path anywhere, on this opcode or otherwise; deposits only happen via the automatic 10-minute
// server-internal tax-skim flush, never a packet. TribeBankHandler now routes sort 2 directly to this
// service's WithdrawAsync (see TribeBankServiceTests.Sort2ThroughHandler_InvokesWithdraw_NotDeposit for the
// handler-level dispatch regression test), superseding the deposit wiring the sibling TribeBankService test
// file's tests once exercised through the handler. TribeBankService.DepositAsync itself is unchanged and
// still covered directly at the service level in that file, but is no longer opcode-reachable. Reuses the
// shared FakeTribeRepository (which zeroes the slot and returns MoneyAfterWithdraw on WithdrawBankAsync).
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
        // Withdraw is Force-Leader-only (role 1); a sub-master (role 2) who may view still cannot withdraw.
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
        // Empty slot (50210) or money-cap breach (50261): the contract disconnects on any non-zero result, so
        // any procedure THROW collapses to Aborted.
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
        repository.MoneyAfterWithdraw = 20_000; // character had 0, drains the whole 20,000 slot

        var service = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);

        var result = await service.WithdrawAsync(9, state, CharacterId, CancellationToken.None);

        Assert.Equal(((byte)1, (byte)9, CharacterId), repository.LastWithdrawCall);
        Assert.True(result.Success);
        Assert.Equal(2, result.Sort);
        Assert.Equal(20_000, result.Money);
        Assert.NotNull(result.TribeBankInfo);
        Assert.Equal(0, result.TribeBankInfo![9]); // slot drained to 0 by the withdraw
    }
}
