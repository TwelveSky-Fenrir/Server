using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.CreateAvatar;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Characters;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Handlers;

// op17 CL_CREATE_AVATAR_SEND2 -- EU33 starter kit (equipment/inventory/skills/hotkeys per tribe, stats,
// welcome buffs, one premium day). Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-1183.
public class CreateAvatarHandlerTests
{
    private const int AccountId = 42;

    private static CreateAvatarRequest ValidRequest(int weapon = 6) => new()
    {
        AvatarPost = 0,
        Tribe = 0,
        PreviousTribe = 0,
        Gender = 1,
        Head = 2,
        Face = 1,
        Weapon = weapon,
        AvatarName = "Hero"
    };

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsTheFullStarterKit()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, _) = CreateSessionInCharSelect();

        var before = DateTimeOffset.UtcNow;
        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(((byte)0, (short)1), starterKits.LastCall); // Tribe 0 -> mapcheck.h map 1

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal(AccountId, call!.AccountId);
        Assert.Equal((byte)0, call.Slot);
        Assert.Equal("Hero", call.Name);
        Assert.Equal((byte)0, call.Tribe);
        Assert.Equal((byte)1, call.Gender);
        Assert.Equal((byte)2, call.HeadType);
        Assert.Equal((byte)1, call.FaceType);
        Assert.Equal((short)1, call.MapId);
        Assert.Equal(6f, call.PosX);
        Assert.Equal(0f, call.PosY);
        Assert.Equal(-7f, call.PosZ);
        Assert.Equal(100, call.Life);
        Assert.Equal(100, call.MaxLife);
        Assert.Equal(50, call.Mana);
        Assert.Equal(50, call.MaxMana);

        // Amulet/Armor/Gloves/Ring/Boots + the chosen Weapon (raw code 6 -> elite 84527, not the other 2
        // alternatives) + universal Cape/Pet. Every elite-gear row carries the SetISIUIMValue(45, 6, 0, 0)
        // encoding (Enchant=45, Combine=6); Cape keeps its own distinct Enchant=40 encoding.
        Assert.Equal(8, call.Equipment.Count);
        Assert.Contains(call.Equipment, i => i is { Slot: 0, ItemId: 84671, Enchant: 45, Combine: 6 });
        Assert.Contains(call.Equipment, i => i is { Slot: 2, ItemId: 84575, Enchant: 45, Combine: 6 });
        Assert.Contains(call.Equipment, i => i is { Slot: 3, ItemId: 84623, Enchant: 45, Combine: 6 });
        Assert.Contains(call.Equipment, i => i is { Slot: 4, ItemId: 84647, Enchant: 45, Combine: 6 });
        Assert.Contains(call.Equipment, i => i is { Slot: 5, ItemId: 84599, Enchant: 45, Combine: 6 });
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 84527, Enchant: 45, Combine: 6 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 84503 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 84551 });
        Assert.Contains(call.Equipment, i => i is { Slot: 1, ItemId: 1407, Enchant: 40 });
        Assert.Contains(call.Equipment, i => i is { Slot: 8, ItemId: 2300 });

        Assert.Equal(4, call.Inventory.Count);
        Assert.Contains(call.Inventory, i => i is { Slot: 0, ItemId: 1026, Quantity: 999 });
        Assert.Contains(call.Inventory, i => i is { Slot: 3, ItemId: 1001, Quantity: 10 });

        Assert.Equal(2, call.Skills.Count);
        Assert.Contains(call.Skills, s => s is { SlotIndex: 0, SkillId: 1, Grade: 1 });

        Assert.Single(call.Hotkeys);
        Assert.Contains(call.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 1, Value1: 1, Value2: 1 });

        // Welcome buff = today + 7 days (YYYYMMDD); premium = now + 1 day (Unix seconds) -- both computed from
        // the wall clock, so asserted as "within the [before,after] call window" rather than an exact literal.
        var expectedWelcomeBuff = DateOnly.FromDateTime(before.UtcDateTime.AddDays(7));
        var actualWelcomeBuff = call.WelcomeBuffUntilDate;
        Assert.Equal(expectedWelcomeBuff.Year * 10000 + expectedWelcomeBuff.Month * 100 + expectedWelcomeBuff.Day,
            actualWelcomeBuff);
        Assert.InRange(call.PremiumUntilUnixSeconds, before.AddDays(1).ToUnixTimeSeconds(),
            after.AddDays(1).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_RepliesResultZeroWithFullAvatarInfo()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        var call = characters.LastCreateWithStarterKit!;
        var createdCharacter = await characters.GetForWorldEntryAsync(1000, CancellationToken.None);
        Assert.NotNull(createdCharacter);

        var expectedAvatarInfo = AvatarInfoFactory.CreateForCharacter(createdCharacter!) with
        {
            Vit = 1,
            Str = 1,
            Int = 1,
            Dex = 1,
            Equip = AvatarInfoFactory.BuildEquipArray(call.Equipment),
            DoubleExpTime1 = call.WelcomeBuffUntilDate,
            DoubleExpTime2 = call.WelcomeBuffUntilDate,
            AutoBuffTime = call.WelcomeBuffUntilDate,
            Premium = call.PremiumUntilUnixSeconds
        };

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 0, AvatarInfo = expectedAvatarInfo });
    }

    // Raw client codes 5/6/7 remap to Noble Dragon's elite weapons (Dragon's Fang Sword/Blade of the Moon/
    // Great Dragon Eye Marble) -- see FakeStarterKitRepository.NobleDragonKit's RawWeaponCode rows.
    [Theory]
    [InlineData(5, 84503)]
    [InlineData(6, 84527)]
    [InlineData(7, 84551)]
    public async Task HandleAsync_AnyOfTheThreeWeaponAlternatives_IsAccepted(int weapon, int eliteItemId)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, _) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(weapon), session, CancellationToken.None);

        Assert.NotNull(characters.LastCreateWithStarterKit);
        Assert.Contains(characters.LastCreateWithStarterKit!.Equipment, i => i.Slot == 7 && i.ItemId == eliteItemId);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_WeaponNotOneOfTheTribesThreeAlternatives_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        // 11 is a valid Royal Serpent weapon id, not one of Noble Dragon's (5/6/7).
        await handler.HandleAsync(ValidRequest(11), session, CancellationToken.None);

        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)] // AvatarPost out of range
    [InlineData(3, 0, 0, 0, 0)] // AvatarPost out of range (> MAX_USER_AVATAR_NUM-1)
    [InlineData(0, -1, 0, 0, 0)] // Tribe out of range
    [InlineData(0, 4, 0, 0, 0)] // Tribe out of range
    [InlineData(0, 0, 0, -1, 0)] // Head out of range
    [InlineData(0, 0, 0, 7, 0)] // Head out of range
    [InlineData(0, 0, 0, 0, -1)] // Face out of range
    [InlineData(0, 0, 0, 0, 3)] // Face out of range
    public async Task HandleAsync_StructuralViolation_AbortsWithoutQueryingOrCreating(
        int avatarPost, int tribe, int previousTribe, int head, int face)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        var request = new CreateAvatarRequest
        {
            AvatarPost = avatarPost,
            Tribe = tribe,
            PreviousTribe = previousTribe,
            Gender = 0,
            Head = head,
            Face = face,
            Weapon = 6,
            AvatarName = "Hero"
        };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    // Server/ts25login/S04_MyWork02.cpp:739-838: the PreviousTribe/race switch has no case-3/default branch,
    // so a PreviousTribe outside 0-2 is a genuine legacy validation gap -- unlike every other field above, it
    // is deliberately NOT range-checked, and the request is not rejected on this basis; only the weapon-matching
    // rule is skipped, leaving the weapon-equip slot unassigned.
    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public async Task HandleAsync_PreviousTribeOutOfRange_CreatesNormallyWithNoWeaponEquipped(int previousTribe)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { PreviousTribe = previousTribe };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.DoesNotContain(call!.Equipment, i => i.Slot == 7); // weapon-equip slot left unassigned

        var createdCharacter = await characters.GetForWorldEntryAsync(1000, CancellationToken.None);
        Assert.NotNull(createdCharacter);
        var expectedAvatarInfo = AvatarInfoFactory.CreateForCharacter(createdCharacter!) with
        {
            Vit = 1,
            Str = 1,
            Int = 1,
            Dex = 1,
            Equip = AvatarInfoFactory.BuildEquipArray(call.Equipment),
            DoubleExpTime1 = call.WelcomeBuffUntilDate,
            DoubleExpTime2 = call.WelcomeBuffUntilDate,
            AutoBuffTime = call.WelcomeBuffUntilDate,
            Premium = call.PremiumUntilUnixSeconds
        };
        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 0, AvatarInfo = expectedAvatarInfo });
    }

    // Server/ts25login/S04_MyWork02.cpp:625-635: the combined slot-occupied/name-empty test silently
    // disconnects the session -- unlike a name already taken (or any other stored-procedure-level failure),
    // which replies with a normal Result=1 response (see HandleAsync_RepositoryThrows_RepliesResult1WithZeroedAvatarInfo).
    [Fact]
    public async Task HandleAsync_SlotAlreadyOccupied_AbortsWithoutQueryingStarterKitOrCreating()
    {
        var characters = FakeCharacterRepository.WithSummaries(
            new CharacterSummaryDto(1000, 0, "Existing", 0, 0, 0, 0, 1));
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None); // AvatarPost = 0, already occupied

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_RepliesResult1WithZeroedAvatarInfo()
    {
        // Slot-occupancy itself is now checked proactively (see HandleAsync_SlotAlreadyOccupied_...) before this
        // call is ever reached -- this exercises the remaining collapse-to-Result=1 catch-all for whatever else
        // usp_Character_CreateWithStarterKit can still throw (e.g. name already taken by another account).
        var characters = FakeCharacterRepository.WithNone();
        characters.CreateWithStarterKitException = new InvalidOperationException("50202: name already taken");
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_EmptyAvatarName_AbortsWithoutQueryingOrCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { AvatarName = "" };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    // CheckNameString (Server/Header/safestring.h:43-81): unlike the structural checks above, a whitelist
    // violation here answers with Result=1 and keeps the session connected (S04_MyWork02.cpp l.658).
    [Theory]
    [InlineData("Hero Knight")] // space
    [InlineData("Hero_Knight")] // underscore
    [InlineData("Hero-Knight")] // hyphen
    [InlineData("Héro")] // accented Latin byte
    public async Task HandleAsync_AvatarNameWithDisallowedCharacters_RepliesResult1WithoutCreatingOrDisconnecting(
        string name)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { AvatarName = name };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
    }

    // ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:250-264: dominant-tribe gate,
    // B_CREATE_AVATAR_RECV Result=3. Tribe 0 (the requested tribe in ValidRequest) is the sole leader at the
    // 100-point floor.
    [Fact]
    public async Task HandleAsync_RequestedTribeIsDominant_RepliesResult3WithZeroedAvatarInfoAndNoPersistence()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var tribes = FakeTribeRepository.WithPoints((0, 100), (1, 0), (2, 0), (3, 0));
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits, tribes, DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 3, AvatarInfo = AvatarInfoFactory.Zeroed });
        Assert.Null(session.DisconnectReason);
        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
    }

    [Fact]
    public async Task HandleAsync_AnotherTribeIsDominant_RequestedTribeStillCreatesNormally()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        // Tribe 1 is dominant (150 points); ValidRequest asks for tribe 0, which this gate never touches --
        // an absolute floor on the leader's own total, not a margin/gap against the other tribes.
        var tribes = FakeTribeRepository.WithPoints((0, 10), (1, 150), (2, 0), (3, 0));
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits, tribes, DefaultOptions()));
        var (session, _) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        Assert.NotNull(characters.LastCreateWithStarterKit);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_LeaderBelowTheFloor_NeverBlocksRegardlessOfImbalance()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var tribes = FakeTribeRepository.WithPoints((0, 99), (1, 0), (2, 0), (3, 0));
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits, tribes, DefaultOptions()));
        var (session, _) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        Assert.NotNull(characters.LastCreateWithStarterKit);
    }

    // Server/ts25login/S04_MyWork02.cpp:640-646: Tribe value 3 (the fourth faction) is rejected outright --
    // same disconnect treatment as an invalid weapon -- unless the operator has re-enabled it. Default
    // (EnableFourthFaction=false) matches legacy's own unconditional shipped behavior.
    [Fact]
    public async Task HandleAsync_TribeThree_ToggleInDefaultDisabledState_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions()));
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { Tribe = 3 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_TribeThree_ToggleOperatorEnabled_CreatesNormally()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                Options.Create(new LoginServerOptions { EnableFourthFaction = true })));
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { Tribe = 3 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Equal(((byte)0, (short)140), starterKits.LastCall); // Tribe 3 -> mapcheck.h map 140

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)3, call!.Tribe);
        Assert.Equal((short)140, call.MapId);
        Assert.Null(session.DisconnectReason);

        var createdCharacter = await characters.GetForWorldEntryAsync(1000, CancellationToken.None);
        Assert.NotNull(createdCharacter);
        var expectedAvatarInfo = AvatarInfoFactory.CreateForCharacter(createdCharacter!) with
        {
            Vit = 1,
            Str = 1,
            Int = 1,
            Dex = 1,
            Equip = AvatarInfoFactory.BuildEquipArray(call.Equipment),
            DoubleExpTime1 = call.WelcomeBuffUntilDate,
            DoubleExpTime2 = call.WelcomeBuffUntilDate,
            AutoBuffTime = call.WelcomeBuffUntilDate,
            Premium = call.PremiumUntilUnixSeconds
        };
        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 0, AvatarInfo = expectedAvatarInfo });
    }

    // EnableFourthFaction defaults to false, matching legacy's own unconditional shipped behavior (see
    // LoginServerOptions' own remarks) -- none of the OTHER requests in this file target Tribe 3, so this
    // only affects FourthFactionGate, never TribeDominanceGate, for those tests.
    private static IOptions<LoginServerOptions> DefaultOptions()
    {
        return Options.Create(new LoginServerOptions());
    }

    private static (LoginClientSession Session, FakeDuplexPipe Pipe) CreateSessionInCharSelect()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(AccountId);
        session.MarkCharSelect();
        return (session, pipe);
    }
}
