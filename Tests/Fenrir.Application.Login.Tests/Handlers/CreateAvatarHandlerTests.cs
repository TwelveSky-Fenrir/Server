using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.CreateAvatar;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

public class CreateAvatarHandlerTests
{
    private const int AccountId = 42;

    private static CreateAvatarRequest ValidRequest(int weapon = 6)
    {
        return new CreateAvatarRequest
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
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsTheBasicStarterKit()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var before = DateTimeOffset.UtcNow;
        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(((byte)0, (short)1), starterKits.LastCall);

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
        Assert.Equal(30, call.Life);
        Assert.Equal(100, call.MaxLife);
        Assert.Equal(21, call.Mana);
        Assert.Equal(50, call.MaxMana);
        Assert.Equal((byte)0, call.PreviousTribe);

        Assert.Equal(4, call.Equipment.Count);
        Assert.Contains(call.Equipment, i => i is { Slot: 2, ItemId: 84575, Enchant: 0, Combine: 0 });
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 84527, Enchant: 0, Combine: 0 });
        Assert.Contains(call.Equipment, i => i is { Slot: 1, ItemId: 1407 });
        Assert.Contains(call.Equipment, i => i is { Slot: 8, ItemId: 2300 });
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 0);
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 3);
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 4);
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 5);
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 84503 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 84551 });

        Assert.Equal(4, call.Inventory.Count);
        Assert.Contains(call.Inventory, i => i is { Slot: 0, ItemId: 1026, Quantity: 999 });
        Assert.Contains(call.Inventory, i => i is { Slot: 3, ItemId: 1001, Quantity: 10 });

        Assert.Equal(2, call.Skills.Count);
        Assert.Contains(call.Skills, s => s is { SlotIndex: 0, SkillId: 1, Grade: 1 });

        Assert.Single(call.Hotkeys);
        Assert.Contains(call.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 1, Value1: 1, Value2: 1 });

        var expectedWelcomeBuff = DateOnly.FromDateTime(before.UtcDateTime.AddDays(7));
        var actualWelcomeBuff = call.WelcomeBuffUntilDate;
        Assert.Equal(expectedWelcomeBuff.Year * 10000 + expectedWelcomeBuff.Month * 100 + expectedWelcomeBuff.Day,
            actualWelcomeBuff);
        Assert.Equal(0L, call.PremiumUntilUnixSeconds);
        _ = after;
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_RepliesResultZeroWithFullAvatarInfo()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
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
            Level2 = 0,
            Exp1 = 0,
            Exp2 = 0,
            StatPoint = 50,
            SkillPoint = 0,
            PreviousTribe = 0,
            Equip = AvatarInfoFactory.BuildEquipArray(call.Equipment, 640_000_000, 100),
            Inventory = AvatarInfoFactory.BuildInventoryArray(call.Inventory),
            Skill = AvatarInfoFactory.BuildSkillArray(call.Skills),
            HotKey = AvatarInfoFactory.BuildHotKeyArray(call.Hotkeys),
            Animal = [1301, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            AnimalIndex = 0,
            AnimalTime = 99999999,
            AnimalPower = [5, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            AutoBuffTime = call.WelcomeBuffUntilDate
        };

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 0, AvatarInfo = expectedAvatarInfo });
    }

    [Theory]
    [InlineData(5, 84503)]
    [InlineData(6, 84527)]
    [InlineData(7, 84551)]
    public async Task HandleAsync_AnyOfTheThreeWeaponAlternatives_IsAccepted(int weapon, int itemId)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(weapon), session, CancellationToken.None);

        Assert.NotNull(characters.LastCreateWithStarterKit);
        Assert.Contains(characters.LastCreateWithStarterKit!.Equipment, i => i.Slot == 7 && i.ItemId == itemId);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_RoyalSerpentPreviousTribe_PersistsItsOwnStarterEquipmentNotNobleDragons()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.RoyalSerpentKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var request = ValidRequest(12) with { Tribe = 1, PreviousTribe = 1 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)1, (short)6),
            starterKits.LastCall);

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)1, call!.Tribe);
        Assert.Equal((short)6, call.MapId);
        Assert.Equal((byte)1, call.PreviousTribe);
        Assert.Equal(4, call.Equipment.Count);
        Assert.Contains(call.Equipment, i => i is { Slot: 2, ItemId: 85575, Enchant: 0, Combine: 0 });
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 85527, Enchant: 0, Combine: 0 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 85503 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 85551 });
        Assert.DoesNotContain(call.Equipment, i => i.ItemId is 84671 or 84575 or 84623 or 84647 or 84599
            or 84503 or 84527 or 84551);
    }

    [Fact]
    public async Task HandleAsync_RoyalSerpentPreviousTribe_WeaponFromAnotherRace_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.RoyalSerpentKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { Tribe = 1, PreviousTribe = 1 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_GrandTigerPreviousTribe_PersistsItsOwnStarterEquipmentNotTheOtherTwoRaces()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.GrandTigerKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var request = ValidRequest(18) with { Tribe = 2, PreviousTribe = 2 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)2, (short)11), starterKits.LastCall);

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)2, call!.Tribe);
        Assert.Equal((short)11, call.MapId);
        Assert.Equal((byte)2, call.PreviousTribe);
        Assert.Equal(4, call.Equipment.Count);
        Assert.Contains(call.Equipment, i => i is { Slot: 2, ItemId: 86575, Enchant: 0, Combine: 0 });
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 86527, Enchant: 0, Combine: 0 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 86503 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 86551 });
        Assert.DoesNotContain(call.Equipment, i => i.ItemId is 84671 or 84575 or 84623 or 84647 or 84599
            or 84503 or 84527 or 84551 or 85671 or 85575 or 85623 or 85647 or 85599 or 85503 or 85527 or 85551);
    }

    [Fact]
    public async Task HandleAsync_GrandTigerPreviousTribe_WeaponFromAnotherRace_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.GrandTigerKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest(12) with { Tribe = 2, PreviousTribe = 2 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task
        HandleAsync_TribeAndPreviousTribeMismatchedWithinMainFactionRange_CreatesNormallyWithNoCrossValidation()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.RoyalSerpentKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var request = ValidRequest(12) with { Tribe = 0, PreviousTribe = 1 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)1, (short)1), starterKits.LastCall);

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)0, call!.Tribe);
        Assert.Equal((short)1, call.MapId);
        Assert.Equal((byte)1, call.PreviousTribe);
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 85527, Enchant: 0, Combine: 0 });
    }

    [Fact]
    public async Task HandleAsync_WeaponNotOneOfTheTribesThreeAlternatives_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(11), session, CancellationToken.None);

        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(3, 0, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 4, 0, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 3, 0, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 7, 0)]
    [InlineData(0, 0, 0, 0, -1)]
    [InlineData(0, 0, 0, 0, 3)]
    public async Task HandleAsync_StructuralViolation_AbortsWithoutQueryingOrCreating(
        int avatarPost, int tribe, int previousTribe, int head, int face)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
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

    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public async Task HandleAsync_PreviousTribeOutOfRange_AbortsAsMalformedWithoutQueryingOrCreating(
        int previousTribe)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { PreviousTribe = previousTribe };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public async Task HandleAsync_GenderOutOfRange_AbortsAsMalformedWithoutQueryingOrCreating(int gender)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { Gender = gender };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public async Task HandleAsync_GenderAtStorageWidthBoundary_CreatesNormallyWithExactValuePersisted(int gender)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var request = ValidRequest() with { Gender = gender };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)gender, call!.Gender);
    }

    [Fact]
    public async Task HandleAsync_SlotAlreadyOccupied_AbortsWithoutQueryingStarterKitOrCreating()
    {
        var characters = FakeCharacterRepository.WithSummaries(
            new CharacterSummaryDto(1000, 0, "Existing", 0, 0, 0, 0, 1));
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrowsGenericError_RepliesResult1WithFullyPopulatedUnpersistedAvatarInfo()
    {
        var characters = FakeCharacterRepository.WithNone();
        characters.CreateWithStarterKitException = new InvalidOperationException("simulated failure");
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 1, AvatarInfo = ExpectedUnpersistedCandidateAvatarInfo(characters) });
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrowsNameAlreadyTakenSqlError_RepliesResult2WithFullyPopulatedUnpersistedAvatarInfo()
    {
        var characters = FakeCharacterRepository.WithNone();
        characters.CreateWithStarterKitException = SqlExceptionTestFactory.WithNumber(50202);
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 2, AvatarInfo = ExpectedUnpersistedCandidateAvatarInfo(characters) });
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrowsUnrelatedSqlError_StillRepliesResult1NotResult2()
    {
        var characters = FakeCharacterRepository.WithNone();
        characters.CreateWithStarterKitException = SqlExceptionTestFactory.WithNumber(50201);
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 1, AvatarInfo = ExpectedUnpersistedCandidateAvatarInfo(characters) });
        Assert.Null(session.DisconnectReason);
    }

    private static AvatarInfo ExpectedUnpersistedCandidateAvatarInfo(FakeCharacterRepository characters)
    {
        var call = characters.LastCreateWithStarterKit!;

        return AvatarInfoFactory.Zeroed with
        {
            Name = "Hero",
            Tribe = 0,
            Gender = 1,
            HeadType = 2,
            FaceType = 1,
            Level1 = 1,
            LogoutInfo = [1, 6, 0, -7, 30, 21],
            Vit = 1,
            Str = 1,
            Int = 1,
            Dex = 1,
            Level2 = 0,
            Exp1 = 0,
            Exp2 = 0,
            StatPoint = 50,
            SkillPoint = 0,
            PreviousTribe = 0,
            Equip = AvatarInfoFactory.BuildEquipArray(call.Equipment, 640_000_000, 100),
            Inventory = AvatarInfoFactory.BuildInventoryArray(call.Inventory),
            Skill = AvatarInfoFactory.BuildSkillArray(call.Skills),
            HotKey = AvatarInfoFactory.BuildHotKeyArray(call.Hotkeys),
            AutoBuffTime = call.WelcomeBuffUntilDate
        };
    }

    [Fact]
    public async Task HandleAsync_EmptyAvatarName_AbortsWithoutQueryingOrCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { AvatarName = "" };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData("Hero Knight")]
    [InlineData("Hero_Knight")]
    [InlineData("Hero-Knight")]
    [InlineData("Héro")]
    public async Task HandleAsync_AvatarNameWithDisallowedCharacters_RepliesResult1WithoutCreatingOrDisconnecting(
        string name)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { AvatarName = name };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
    }

    [Fact]
    public async Task HandleAsync_RequestedTribeIsDominant_RepliesResult3WithZeroedAvatarInfoAndNoPersistence()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var tribes = FakeTribeRepository.WithPoints((0, 100), (1, 0), (2, 0), (3, 0));
        var handler =
            new CreateAvatarHandler(
                new CreateAvatarService(characters, starterKits, tribes,
                    NullLogger<CreateAvatarService>.Instance),
                NullLogger<CreateAvatarHandler>.Instance);
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
        var tribes = FakeTribeRepository.WithPoints((0, 10), (1, 150), (2, 0), (3, 0));
        var handler =
            new CreateAvatarHandler(
                new CreateAvatarService(characters, starterKits, tribes,
                    NullLogger<CreateAvatarService>.Instance),
                NullLogger<CreateAvatarHandler>.Instance);
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
        var handler =
            new CreateAvatarHandler(
                new CreateAvatarService(characters, starterKits, tribes,
                    NullLogger<CreateAvatarService>.Instance),
                NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        Assert.NotNull(characters.LastCreateWithStarterKit);
    }

    [Fact]
    public async Task HandleAsync_TribeThree_UnconditionallyAbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { Tribe = 3 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(starterKits.LastCall);
        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
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
