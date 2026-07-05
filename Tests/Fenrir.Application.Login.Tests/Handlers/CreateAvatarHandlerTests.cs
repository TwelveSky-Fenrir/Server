using Fenrir.Application.Login.Avatars;
using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

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
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits));
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

        // Armor/Gloves/Boots + the chosen Weapon (6, not the other 2 alternatives) + universal Cape/Pet.
        Assert.Equal(6, call.Equipment.Count);
        Assert.Contains(call.Equipment, i => i is { Slot: 2, ItemId: 8 });
        Assert.Contains(call.Equipment, i => i is { Slot: 3, ItemId: 9 });
        Assert.Contains(call.Equipment, i => i is { Slot: 5, ItemId: 10 });
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 6 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 5 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 7 });
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
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits));
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

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task HandleAsync_AnyOfTheThreeWeaponAlternatives_IsAccepted(int weapon)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits));
        var (session, _) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(weapon), session, CancellationToken.None);

        Assert.NotNull(characters.LastCreateWithStarterKit);
        Assert.Contains(characters.LastCreateWithStarterKit!.Equipment, i => i.Slot == 7 && i.ItemId == weapon);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_WeaponNotOneOfTheTribesThreeAlternatives_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits));
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
    [InlineData(0, 0, -1, 0, 0)] // PreviousTribe out of range
    [InlineData(0, 0, 3, 0, 0)] // PreviousTribe out of range
    [InlineData(0, 0, 0, -1, 0)] // Head out of range
    [InlineData(0, 0, 0, 7, 0)] // Head out of range
    [InlineData(0, 0, 0, 0, -1)] // Face out of range
    [InlineData(0, 0, 0, 0, 3)] // Face out of range
    public async Task HandleAsync_StructuralViolation_AbortsWithoutQueryingOrCreating(
        int avatarPost, int tribe, int previousTribe, int head, int face)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits));
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

    [Fact]
    public async Task HandleAsync_RepositoryThrows_RepliesResult1WithZeroedAvatarInfo()
    {
        var characters = FakeCharacterRepository.WithNone();
        characters.CreateWithStarterKitException = new InvalidOperationException("50201: slot already occupied");
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(new CreateAvatarService(characters, starterKits));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
        Assert.Null(session.DisconnectReason);
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
