using System.Buffers.Binary;
using Fenrir.Application.Game.Handlers.Tribes;
using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Tribes;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeBankHandlerTests
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
        var (session, _, _) = Setup(zone, 1, 0);
        var handler =
            new TribeBankHandler(new TribeBankService(new FakeTribeRepository(), NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 1, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task View_WithTribeRole_ReturnsBankArray()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 2, 2);
        var repository = new FakeTribeRepository();
        repository.Bank[(2, 5)] = 7_000;
        repository.Bank[(2, 49)] = 12;
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 1, Value = 0 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<TribeBankResponse>(), frame.Length);

        var payload = frame.AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(7_000, BinaryPrimitives.ReadInt32LittleEndian(payload[(8 + 5 * 4)..]));
        Assert.Equal(12, BinaryPrimitives.ReadInt32LittleEndian(payload[(8 + 49 * 4)..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[208..]));
    }

    [Fact]
    public async Task Withdraw_NotForceLeader_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 2);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 2, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_NotEnoughSubMasters_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.Add(new TribeSubMasterDto(1, 0, 100));
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 2, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Null(repository.LastWithdrawCall);
    }

    [Fact]
    public async Task Withdraw_RepositoryThrows_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        repository.WithdrawException = new InvalidOperationException("insufficient tribe bank balance");
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 2, Value = 4 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Withdraw_Success_ReturnsUpdatedBankAndMoney()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 1, 1);
        var repository = new FakeTribeRepository();
        repository.SubMasters.AddRange(
        [
            new TribeSubMasterDto(1, 0, 100), new TribeSubMasterDto(1, 1, 101), new TribeSubMasterDto(1, 2, 102)
        ]);
        repository.Bank[(1, 4)] = 50_000;
        repository.MoneyAfterWithdraw = 999_999;
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 2, Value = 4 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)1, (byte)4, CharacterId), repository.LastWithdrawCall);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        var payload = frame.AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[(8 + 4 * 4)..]));
        Assert.Equal(999_999, BinaryPrimitives.ReadInt32LittleEndian(payload[208..]));
    }

    [Fact]
    public async Task UnknownSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 1);
        var handler =
            new TribeBankHandler(new TribeBankService(new FakeTribeRepository(), NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 4, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Deposit_SlotIndexOutOfRange_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository();
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 3, Value = 50 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Null(repository.LastDepositCall);
    }

    [Fact]
    public async Task Deposit_RegularMemberWithNoTribeRole_StillAllowed()
    {
        // Unlike view/withdraw, deposit only ever moves the depositor's own money -- no privileged role
        // (TribeRole == 0, i.e. not master/sub-master/vote-candidate) is required.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository { MoneyAfterDeposit = 0, DepositAmount = 5_000 };
        repository.Bank[(1, 4)] = 0;
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 3, Value = 4 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)1, (byte)4, CharacterId), repository.LastDepositCall);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<TribeBankResponse>(), frame.Length);
    }

    [Fact]
    public async Task Deposit_RepositoryThrows_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository
        {
            DepositException = new InvalidOperationException("character has no money to deposit")
        };
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 3, Value = 4 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Deposit_Success_ReturnsUpdatedBankAndMoney()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 1, 0);
        var repository = new FakeTribeRepository { MoneyAfterDeposit = 0, DepositAmount = 30_000 };
        repository.Bank[(1, 9)] = 20_000;
        var handler = new TribeBankHandler(new TribeBankService(repository, NullLogger<TribeBankService>.Instance));

        await handler.HandleAsync(new TribeBankRequest { Sort = 3, Value = 9 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)1, (byte)9, CharacterId), repository.LastDepositCall);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        var payload = frame.AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(50_000, BinaryPrimitives.ReadInt32LittleEndian(payload[(8 + 9 * 4)..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[208..]));
    }
}
