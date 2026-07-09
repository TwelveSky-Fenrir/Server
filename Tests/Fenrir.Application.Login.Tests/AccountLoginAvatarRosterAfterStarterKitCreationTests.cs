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

/// <summary>
///     Confirmed-root-cause regression test for the user's exact reported scenario: the character-select
///     roster (LC_USER_AVATAR_RECV2, sent on every CL_LOGIN_SEND) used to always show a returning character's
///     equipment/inventory/core stats as zero, regardless of what was actually persisted in
///     game.Characters/game.CharacterItems -- <see cref="LoginTrain.BuildAvatarSlots" /> only ever overlaid
///     Tribe/Gender/HeadType/FaceType/Level1/Name/GuildName onto <see cref="LoginTrain" />'s zeroed
///     EmptyAvatarSlot template, leaving Equip/Inventory/StoreItem/every progression scalar at zero forever.
///     <para>
///         This test drives the real production code on both sides of that gap, end to end, with no I/O:
///         Step 1 uses the actual <see cref="CreateAvatarService" /> (via <see cref="CreateAvatarHandler" />)
///         to grant a genuine weapon + torso-armor starter kit -- the exact "weapon+torso equipped" scenario
///         reported -- exactly like a real CL_CREATE_AVATAR_SEND2 would (see CreateAvatarService's own
///         &lt;remarks&gt;: ArmorEquipSlot=2/WeaponEquipSlot=7, nothing else granted under the current
///         Level-1-character design). Step 2 simulates a disconnect and a second CL_LOGIN_SEND: a fresh
///         <see cref="FakeCharacterRepository" /> stands in for "the database now durably holds what Step 1
///         just asked usp_Character_CreateWithStarterKit to persist", seeded directly from Step 1's own
///         captured equipment rows (never hand-picked constants) the same shape
///         usp_Character_GetAccountRoster's RS1 would return them in. <see cref="LoginHandler" /> is then
///         driven exactly like a real reconnect, through <c>ICharacterRepository.GetAccountRosterAsync</c>-
///         backed roster resolution (not <c>GetForWorldEntryAsync</c>, the world-entry path) and
///         <see cref="LoginTrain.BuildAvatarSlots" />.
///     </para>
///     <para>
///         Before the fix this assertion would have failed: the second login's AvatarRosterResponse.Equip
///         array would have been all zeros for this character regardless of what Step 1 actually granted.
///     </para>
/// </summary>
public class AccountLoginAvatarRosterAfterStarterKitCreationTests
{
    private const int AccountId = 7;
    private const int ClientVersion = 90354; // LoginServerOptions.ExpectedClientVersion default

    private const byte
        WeaponEquipSlot =
            7; // CreateAvatarService.WeaponEquipSlot / AvatarInfoFactory.PetEquipSlot's own sibling constant

    private const byte ArmorEquipSlot = 2; // CreateAvatarService.ArmorEquipSlot (torso)
    private const byte EquipmentContainer = 2; // AvatarInfoFactory.ContainerEquipment convention

    [Fact]
    public async Task
        SecondLogin_AfterCharacterCreationWithStarterKit_RosterShowsRealWeaponAndTorsoArmorInsteadOfZeros()
    {
        // --- Step 1: create a character through the real CreateAvatarService production path -- a basic
        // weapon + torso-armor starter kit, exactly like a genuine CL_CREATE_AVATAR_SEND2 grants.
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
            Weapon = 6, // raw code 6 -> Blade of the Moon (84527), Noble Dragon
            AvatarName = "Hero"
        }, creationSession, CancellationToken.None);

        Assert.Null(creationSession.DisconnectReason);
        var creationCall = creationRepository.LastCreateWithStarterKit;
        Assert.NotNull(creationCall);

        // Confirms this really is the "weapon+torso" scenario reported, sourced from the actual production
        // starter-kit-granting code, not a hand-picked item list: exactly these two equip rows, nothing else.
        Assert.Equal(2, creationCall!.Equipment.Count);
        var weaponRow = Assert.Single(creationCall.Equipment, i => i.Slot == WeaponEquipSlot);
        var torsoRow = Assert.Single(creationCall.Equipment, i => i.Slot == ArmorEquipSlot);
        Assert.Equal(84527, weaponRow.ItemId);
        Assert.Equal(84575, torsoRow.ItemId);

        var createdCharacter = await creationRepository.GetForWorldEntryAsync(1000, CancellationToken.None);
        Assert.NotNull(createdCharacter);
        var characterId = createdCharacter!.CharacterId;

        // --- Step 2: simulate a disconnect and a second CL_LOGIN_SEND. A fresh repository stands in for "the
        // database now durably holds what Step 1 just asked usp_Character_CreateWithStarterKit to persist" --
        // seeded from creationCall's own equipment rows, read back exactly how
        // ICharacterRepository.GetAccountRosterAsync (usp_Character_GetAccountRoster) would on a real
        // reconnect -- deliberately NOT GetForWorldEntryAsync/GetForWorldEntryBundleAsync, the world-entry
        // path this bug never affected.
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

        // The confirmed fix, spelled out as explicit value assertions (not just a byte match against a
        // second call to the same production code): a returning character's roster slot now shows the real
        // equipped weapon and torso armor -- not the zeroed EmptyAvatarSlot template every login used to
        // send regardless of what was actually persisted.
        Assert.Equal(84527, expectedSlots[0].Equip[WeaponEquipSlot * 4]);
        Assert.Equal(84575, expectedSlots[0].Equip[ArmorEquipSlot * 4]);
        Assert.Equal("Hero", expectedSlots[0].Name);

        // Negative assertion: every OTHER equip slot must still be zero (only the two starter-kit rows were
        // ever granted) -- guards against a regression that overlaid the whole array with garbage instead of
        // the real per-slot data.
        for (var slotIndex = 0; slotIndex < 13; slotIndex++)
        {
            if (slotIndex == WeaponEquipSlot || slotIndex == ArmorEquipSlot)
                continue;
            Assert.Equal(0, expectedSlots[0].Equip[slotIndex * 4]);
        }

        // Inventory/StoreItem stay zero too -- the starter kit's inventory/store rows are deliberately not
        // seeded on reloginRepository (this scenario is about the equipped weapon+torso specifically); this
        // guards against a regression that stamped non-empty data onto containers this test never populated.
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
