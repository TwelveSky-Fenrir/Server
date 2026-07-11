using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers.Tribes;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeBankServiceTests
{
    private const int CharacterId = 42;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        byte tribe, byte tribeRole, short accountGrade = 0)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId, accountGrade: accountGrade);
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
        var (session, _, state) = Setup(zone, 1, 0);
        var service = new TribeBankService(new FakeTribeRepository(), NullLogger<TribeBankService>.Instance);

        var result = await service.ViewAsync(session, state, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task View_WithTribeRole_ReturnsBankArray()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 2, 2);
        var repository = new FakeTribeRepository();
        repository.Bank[(2, 5)] = 7_000;
        repository.Bank[(2, 49)] = 12;
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.ViewAsync(session, state, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Sort);
        Assert.NotNull(result.TribeBankInfo);
        Assert.Equal(7_000, result.TribeBankInfo![5]);
        Assert.Equal(12, result.TribeBankInfo[49]);
        Assert.Equal(0, result.Money);
    }

    [Fact]
    public async Task View_NoTribeRoleButStaffTier_BypassesGateAndReturnsBankArray()
    {
        // uUserSort < 1 GM bypass (Server/ts25zone/S04_MyWork02.cpp:11560-11607): a staff/GM-tier caller
        // (GmCommandTier.Basic or higher) skips the ReturnTribeRole != 0 check entirely and can view any
        // tribe's bank, scoped to whatever tribe id is recorded on their own avatar.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 3, 0, 1);
        var repository = new FakeTribeRepository();
        repository.Bank[(3, 2)] = 4_500;
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.ViewAsync(session, state, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Sort);
        Assert.NotNull(result.TribeBankInfo);
        Assert.Equal(4_500, result.TribeBankInfo![2]);
    }

    [Fact]
    public async Task UnknownSort_Aborts()
    {
        // Sort 1/2 dispatch to View/Withdraw (via TribeBankWithdrawService) respectively; any other sort --
        // including the removed, never-legacy Sort 3 a previous revision of this handler mistakenly
        // recognized as "deposit" -- is a handler-owned fallback neither service ever sees; exercise the
        // real handler here.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);
        var withdrawService = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);
        var handler = new TribeBankHandler(service, withdrawService);

        await handler.HandleAsync(new TribeBankRequest { Sort = 4, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task FabricatedSort3_NoLongerRecognized_Aborts()
    {
        // Regression test for the confirmed audit gap: there is no legacy Sort 3 on this opcode. Sort 3 must
        // hit the same unrecognized-sub-command fallback as any other invalid value, and must invoke neither
        // the (now-orphaned) deposit path nor the withdraw path.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);
        var withdrawService = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);
        var handler = new TribeBankHandler(service, withdrawService);

        await handler.HandleAsync(new TribeBankRequest { Sort = 3, Value = 4 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Null(repository.LastDepositCall);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Sort2ThroughHandler_InvokesWithdraw_NotDeposit()
    {
        // Regression test for the confirmed economy bug this fix closes: a fresh, definitive full read of
        // Server/ts25zone/S04_MyWork02.cpp:11560-11607 and Server/ts25playuser/S04_MyWork02.cpp:269-377
        // resolved a 3-way source contradiction -- CZ_TRIBE_BANK_SEND sort 2 is EXCLUSIVELY a withdraw
        // (bank slot -> player money). Legacy has no client-invocable deposit path anywhere; deposits only
        // happen via the automatic 10-minute server-internal tax-skim flush, never a packet. This test used
        // to assert the opposite (that sort 2 invoked deposit), which was itself the bug.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository { MoneyAfterWithdraw = 12_345 };
        repository.Bank[(1, 4)] = 12_345;
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);
        var withdrawService = new TribeBankWithdrawService(repository, NullLogger<TribeBankWithdrawService>.Instance);
        var handler = new TribeBankHandler(service, withdrawService);

        await handler.HandleAsync(new TribeBankRequest { Sort = 2, Value = 4 }, session, CancellationToken.None);

        Assert.Equal(((byte)1, (byte)4, CharacterId), repository.LastWithdrawCall);
        Assert.Null(repository.LastDepositCall);
        Assert.Null(session.DisconnectReason);
        ZoneTestKit.DrainOutbound(pipe);
    }

    // The following tests exercise TribeBankService.DepositAsync directly at the service level. As of the
    // sort-2 correction above, this method is no longer reachable from any opcode (there is no legacy
    // client-invocable deposit path at all) -- it is retained, not deleted, pending a separate decision on
    // whether to remove it outright, so these tests intentionally keep covering its still-existing behavior.
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
    public async Task Deposit_NotForceLeader_Aborts()
    {
        // Legacy's one mutating tribe-bank operation (Server/ts25zone/S04_MyWork02.cpp:11560-11607) requires
        // Force Leader role plus a 3-sub-master quorum regardless of a sub-master's ability to view --
        // sub-masters can view the bank but never deposit into it.
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 2);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastDepositCall);
    }

    [Fact]
    public async Task Deposit_RegularMemberWithNoTribeRole_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository();
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastDepositCall);
    }

    [Fact]
    public async Task Deposit_NotEnoughSubMasters_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.Add(new TribeSubMasterDto(1, 0, 100));
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(repository.LastDepositCall);
    }

    [Fact]
    public async Task Deposit_RepositoryThrows_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository
        {
            DepositException = new InvalidOperationException("character has no money to deposit")
        };
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        var service = new TribeBankService(repository, NullLogger<TribeBankService>.Instance);

        var result = await service.DepositAsync(4, state, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Deposit_Success_ReturnsUpdatedBankAndMoney()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository { MoneyAfterDeposit = 0, DepositAmount = 30_000 };
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
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
