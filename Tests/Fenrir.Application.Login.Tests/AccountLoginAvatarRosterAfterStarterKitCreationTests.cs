using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Domain.RateLimiting;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.CreateAvatar;
using Fenrir.Application.Login.Services.Login;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Security;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests;

public class AccountLoginAvatarRosterAfterStarterKitCreationTests
{
    private const int AccountId = 7;
    private const int ClientVersion = 90354;

    private const byte
        WeaponEquipSlot =
            7;

    private const byte ArmorEquipSlot = 2;
    private const byte EquipmentContainer = 2;

    [Fact]
    public async Task
        SecondLogin_AfterCharacterCreationWithStarterKit_RosterShowsRealWeaponAndTorsoArmorInsteadOfZeros()
    {
        var creationRepository = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var createAvatarHandler = new CreateAvatarHandler(
            new CreateAvatarService(creationRepository, starterKits, FakeTribeRepository.Empty(),
                Options.Create(new LoginServerOptions()), NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);

        var creationPipe = new FakeDuplexPipe();
        var creationSession = new LoginClientSession(1, creationPipe);
        creationSession.MarkAuthenticated(AccountId);
        creationSession.MarkCharSelect();

        await createAvatarHandler.HandleAsync(new CreateAvatarRequest
        {
            AvatarPost = 0,
            Tribe = 0,
            PreviousTribe = 0,
            Gender = 1,
            Head = 2,
            Face = 1,
            Weapon = 6,
            AvatarName = "Hero"
        }, creationSession, CancellationToken.None);

        Assert.Null(creationSession.DisconnectReason);
        var creationCall = creationRepository.LastCreateWithStarterKit;
        Assert.NotNull(creationCall);

        Assert.Equal(2, creationCall!.Equipment.Count);
        var weaponRow = Assert.Single(creationCall.Equipment, i => i.Slot == WeaponEquipSlot);
        var torsoRow = Assert.Single(creationCall.Equipment, i => i.Slot == ArmorEquipSlot);
        Assert.Equal(84527, weaponRow.ItemId);
        Assert.Equal(84575, torsoRow.ItemId);

        var createdCharacter = await creationRepository.GetForWorldEntryAsync(1000, CancellationToken.None);
        Assert.NotNull(createdCharacter);
        var characterId = createdCharacter!.CharacterId;

        var summary = new CharacterSummaryDto(characterId, 0, "Hero", 0, 1,
            2, 1, 1);
        var rosterCharacter = new CharacterRosterDto(
            characterId, 0, "Hero", 0, 0, 1, 2,
            1, 1, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 1, 6f, 0f, -7f, 30, 21);

        var reloginRepository = FakeCharacterRepository.WithSummaries(summary)
            .WithRosterCharacter(rosterCharacter)
            .WithRosterItem(ToRosterItem(characterId, weaponRow))
            .WithRosterItem(ToRosterItem(characterId, torsoRow));

        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(AccountId, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);

        var loginHandler = CreateLoginHandler(accounts, reloginRepository);
        var loginPipe = new FakeDuplexPipe();
        var loginSession = new LoginClientSession(2, loginPipe);

        await loginHandler.HandleAsync(new LoginRequest
        {
            Id = "hero-account",
            Password = "correct-password",
            Version = ClientVersion,
            Adapter = new LoginAdapterInfo
            {
                AdapterName = "{real-adapter-guid}",
                PhysicalAddressLength = 6,
                PhysicalAddress = Pad8([0x11, 0x22, 0x33, 0x44, 0x55, 0x66]),
                IPAddress = ""
            }
        }, loginSession, CancellationToken.None);

        Assert.NotNull(loginSession.AccountId);

        var actual = await PacketAssert.ReadSentBytesAsync(loginPipe);

        var expectedSlots = LoginTrain.BuildAvatarSlots([
            new AvatarRosterEntry(rosterCharacter,
                [ToRosterItem(characterId, weaponRow), ToRosterItem(characterId, torsoRow)],
                "", new Dictionary<byte, string>(), "", "")
        ]);

        var loginRecvSize = FrameWriter.FrameSizeOf<LoginResponse>();
        var avatarSlotSize = FrameWriter.FrameSizeOf<AvatarRosterResponse>();
        var expectedSlot0Frame = new byte[avatarSlotSize];
        FrameWriter.WriteFrame(in expectedSlots[0], expectedSlot0Frame);

        Assert.Equal(expectedSlot0Frame, actual.AsSpan(loginRecvSize, avatarSlotSize).ToArray());

        Assert.Equal(84527, expectedSlots[0].Equip[WeaponEquipSlot * 4]);
        Assert.Equal(84575, expectedSlots[0].Equip[ArmorEquipSlot * 4]);
        Assert.Equal("Hero", expectedSlots[0].Name);

        for (var slotIndex = 0; slotIndex < 13; slotIndex++)
        {
            if (slotIndex == WeaponEquipSlot || slotIndex == ArmorEquipSlot)
                continue;
            Assert.Equal(0, expectedSlots[0].Equip[slotIndex * 4]);
        }

        Assert.All(expectedSlots[0].Inventory, value => Assert.Equal(0, value));
        Assert.All(expectedSlots[0].StoreItem, value => Assert.Equal(0, value));
    }

    private static CharacterRosterItemDto ToRosterItem(int characterId, CharacterItemSlotTvp row)
    {
        return new CharacterRosterItemDto(characterId, EquipmentContainer, row.Slot, row.ItemId, row.Quantity,
            row.Enchant, row.Combine, row.Refine, row.Socket, row.SocketGem1, row.SocketGem2, row.SocketGem3,
            row.ExpireDate, row.Serial);
    }

    private static LoginHandler CreateLoginHandler(FakeAccountRepository accounts,
        FakeCharacterRepository characters)
    {
        var gmAllowlistRepository = new FakeGmAllowlistRepository();
        var firewall = new ApplicationFirewall(
            new FakeBlockedIpRepository(),
            new FakeFirewallRuleRepository(),
            gmAllowlistRepository);

        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(10_000);
        capacity.SetCurrentPlayers(0);

        return new LoginHandler(
            new LoginService(
                accounts,
                FakeAccountPinRepository.WithNoPin(),
                characters,
                new LoginIpRateLimiter(),
                capacity,
                firewall,
                gmAllowlistRepository,
                new FakeBanRepository(),
                new FakeMacRestrictionRepository(),
                Options.Create(new LoginServerOptions { ExpectedClientVersion = ClientVersion }),
                new SessionRegistry(),
                new FakeAccountSessionRepository(),
                new FakeEventLogRepository(),
                FakeGuildRepository.Empty(),
                FakeFriendRepository.Empty(),
                FakeMentorRepository.Empty(),
                NullLogger<LoginService>.Instance),
            NullLogger<LoginHandler>.Instance);
    }

    private static byte[] Pad8(byte[] address)
    {
        var padded = new byte[8];
        address.AsSpan().CopyTo(padded);
        return padded;
    }
}
