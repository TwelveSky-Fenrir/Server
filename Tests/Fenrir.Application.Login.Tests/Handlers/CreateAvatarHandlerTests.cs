using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.CreateAvatar;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Handlers;

// op17 CL_CREATE_AVATAR_SEND2 -- character-creation-level1-redesign (CONFIRMED PRODUCT DECISION, see
// CreateAvatarService's own <remarks>): a fresh character starts at Level 1 with a basic weapon + torso-armor
// kit only, not the EU33 instant-elite grant this file used to assert. Réf. C++ : Server/ts25login/
// S04_MyWork02.cpp:582-1183 (preconditions/weapon-remap only -- the instant-elite/mount/pet/cape/premium
// grant that same block also compiles is deliberately NOT replicated anymore).
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
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
        // S04_MyWork02.cpp:1096-1097: current life/mana are 30/21 -- unaffected by the level-1 redesign, see
        // CreateAvatarService.StartLife/StartMana's own remarks. MaxLife/MaxMana have no creation-time legacy
        // value at all (recomputed dynamically on world entry), so these two remain an unresolved placeholder
        // pending a dedicated MyFactor-formula contract.
        Assert.Equal(30, call.Life);
        Assert.Equal(100, call.MaxLife);
        Assert.Equal(21, call.Mana);
        Assert.Equal(50, call.MaxMana);
        // Confirms CreateAvatarService now passes the request's own PreviousTribe through explicitly (see
        // HandleAsync_RoyalSerpentPreviousTribe_.../_GrandTiger_... below for non-zero values) instead of
        // relying on ICharacterRepository.CreateWithStarterKitAsync's byte previousTribe = 0 default.
        Assert.Equal((byte)0, call.PreviousTribe);

        // Exactly the chosen Weapon (raw code 6 -> 84527, not the other 2 alternatives) + the tribe's own
        // Armor/torso row -- Amulet/Gloves/Ring/Boots and the old universal Cape/Pet are no longer granted at
        // all. Both remaining rows are unenchanted/uncombined (Enchant 0, Combine 0), not the old
        // SetISIUIMValue(45, 6, 0, 0) elite-gear stamp.
        Assert.Equal(2, call.Equipment.Count);
        Assert.Contains(call.Equipment, i => i is { Slot: 2, ItemId: 84575, Enchant: 0, Combine: 0 });
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 84527, Enchant: 0, Combine: 0 });
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 0); // Amulet
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 3); // Gloves
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 4); // Ring
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 5); // Boots
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 1); // Cape
        Assert.DoesNotContain(call.Equipment, i => i.Slot == 8); // Pet
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 84503 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 84551 });

        Assert.Equal(4, call.Inventory.Count);
        Assert.Contains(call.Inventory, i => i is { Slot: 0, ItemId: 1026, Quantity: 999 });
        Assert.Contains(call.Inventory, i => i is { Slot: 3, ItemId: 1001, Quantity: 10 });

        Assert.Equal(2, call.Skills.Count);
        Assert.Contains(call.Skills, s => s is { SlotIndex: 0, SkillId: 1, Grade: 1 });

        Assert.Single(call.Hotkeys);
        Assert.Contains(call.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 1, Value1: 1, Value2: 1 });

        // Welcome buff = today + 7 days (YYYYMMDD) -- unaffected by the level-1 redesign, computed from the
        // wall clock so asserted as "within the [before,after] call window" rather than an exact literal.
        var expectedWelcomeBuff = DateOnly.FromDateTime(before.UtcDateTime.AddDays(7));
        var actualWelcomeBuff = call.WelcomeBuffUntilDate;
        Assert.Equal(expectedWelcomeBuff.Year * 10000 + expectedWelcomeBuff.Month * 100 + expectedWelcomeBuff.Day,
            actualWelcomeBuff);
        // No premium-day grant anymore (see CreateAvatarService.NoPremiumGrant's own remarks) -- @before/@after
        // are still captured above purely to bound the welcome-buff date computation.
        Assert.Equal(0L, call.PremiumUntilUnixSeconds);
        _ = after;
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_RepliesResultZeroWithFullAvatarInfo()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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
            // A genuine Level 1 character: no post-cap "high level" ladder progress (Level2), no experience of
            // either kind (Exp1/Exp2) -- see CreateAvatarService's own remarks. StatPoint/SkillPoint are Fenrir
            // product defaults (NOT legacy-cited), see CreateAvatarService.StartingStatPoint/
            // StartingSkillPoint's own remarks.
            Level2 = 0,
            Exp1 = 0,
            Exp2 = 0,
            StatPoint = 50,
            SkillPoint = 0,
            PreviousTribe = 0,
            // No petGrowth/petActivity: BuildEquipArray's own defaults (0/0) apply -- no pet is ever granted
            // under this redesign.
            Equip = AvatarInfoFactory.BuildEquipArray(call.Equipment),
            Inventory = AvatarInfoFactory.BuildInventoryArray(call.Inventory),
            Skill = AvatarInfoFactory.BuildSkillArray(call.Skills),
            HotKey = AvatarInfoFactory.BuildHotKeyArray(call.Hotkeys),
            // AutoBuffTime is the only remaining "known at creation time" overlay -- Animal*/ProtectForDeath/
            // DoubleExpTime1-2/AutoTime2/Premium are deliberately left at AvatarInfoFactory.Zeroed's own 0/
            // empty-array defaults (see CreateAvatarService's own remarks): this redesign grants none of them.
            AutoBuffTime = call.WelcomeBuffUntilDate
        };

        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 0, AvatarInfo = expectedAvatarInfo });
    }

    // Raw client codes 5/6/7 remap to Noble Dragon's own weapons (Dragon's Fang Sword/Blade of the Moon/Great
    // Dragon Eye Marble) -- see FakeStarterKitRepository.NobleDragonKit's RawWeaponCode rows.
    [Theory]
    [InlineData(5, 84503)]
    [InlineData(6, 84527)]
    [InlineData(7, 84551)]
    public async Task HandleAsync_AnyOfTheThreeWeaponAlternatives_IsAccepted(int weapon, int itemId)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        await handler.HandleAsync(ValidRequest(weapon), session, CancellationToken.None);

        Assert.NotNull(characters.LastCreateWithStarterKit);
        Assert.Contains(characters.LastCreateWithStarterKit!.Equipment, i => i.Slot == 7 && i.ItemId == itemId);
        Assert.Null(session.DisconnectReason);
    }

    // Adversarial gap fix: every other "starter equipment" assertion in this file went through NobleDragonKit
    // (PreviousTribe 0) exclusively -- PreviousTribe 1 (Royal Serpent) and 2 (Grand Tiger) had no handler-level
    // coverage at all asserting THEIR OWN item ids, even though the pass-through logic in CreateAvatarService
    // (BuildEquipmentRows et al.) is generic across races and the "previousTribe is 0 or 1 or 2"
    // weapon-validation gate (S04_MyWork02.cpp:739-838) was only ever exercised at value 0 (true branch) and
    // 3/255 (false branch) -- never at 1 or 2. This closes both gaps at once: a regression that narrowed that
    // gate to `previousTribe is 0` only, or one that let one race's item ids leak into another's flow, would go
    // undetected without these two tests (mirrors StarterKitProcTests.GetByPreviousTribeAsync_RoyalSerpent_.../
    // _GrandTiger_... at the DB layer, using the exact same seeded ids so a mismatch between the two layers
    // would be self-evident).
    [Fact]
    public async Task HandleAsync_RoyalSerpentPreviousTribe_PersistsItsOwnStarterEquipmentNotNobleDragons()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.RoyalSerpentKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var request = ValidRequest(12) with { Tribe = 1, PreviousTribe = 1 }; // raw code 12 -> Black Feast

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)1, (short)6),
            starterKits.LastCall); // Tribe 1 -> mapcheck.h map 6; PreviousTribe threaded unchanged

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)1, call!.Tribe);
        Assert.Equal((short)6, call.MapId);
        // Confirms PreviousTribe (1, independent of Tribe's own value) reaches the repository unchanged,
        // matching Server/ts25zone/S04_MyWork02.cpp:880-901's expectation that the two stay in lockstep here.
        Assert.Equal((byte)1, call.PreviousTribe);
        Assert.Equal(2, call.Equipment.Count);
        Assert.Contains(call.Equipment, i => i is { Slot: 2, ItemId: 85575, Enchant: 0, Combine: 0 });
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 85527, Enchant: 0, Combine: 0 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 85503 });
        Assert.DoesNotContain(call.Equipment, i => i is { Slot: 7, ItemId: 85551 });
        // Guards against a seed/catalog mixup leaking Noble Dragon's ids into this race's flow.
        Assert.DoesNotContain(call.Equipment, i => i.ItemId is 84671 or 84575 or 84623 or 84647 or 84599
            or 84503 or 84527 or 84551);
    }

    [Fact]
    public async Task HandleAsync_RoyalSerpentPreviousTribe_WeaponFromAnotherRace_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.RoyalSerpentKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        // 6 is a valid Noble Dragon weapon id, not one of Royal Serpent's (11/12/13) -- guards the
        // "previousTribe is 0 or 1 or 2" weapon-validation gate at value 1 specifically, not just 0.
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var request = ValidRequest(18) with { Tribe = 2, PreviousTribe = 2 }; // raw code 18 -> Qing Long's Grace

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(((byte)2, (short)11), starterKits.LastCall); // Tribe 2 -> mapcheck.h map 11

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)2, call!.Tribe);
        Assert.Equal((short)11, call.MapId);
        Assert.Equal((byte)2, call.PreviousTribe);
        Assert.Equal(2, call.Equipment.Count);
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        // 12 is a valid Royal Serpent weapon id, not one of Grand Tiger's (17/18/19) -- guards the same gate
        // at value 2.
        var request = ValidRequest(12) with { Tribe = 2, PreviousTribe = 2 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(characters.LastCreateWithStarterKit);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    // Adversarial gap fix: CreateAvatarService's own remarks document that creation deliberately never
    // cross-checks Tribe against PreviousTribe (Server/ts25login/S04_MyWork02.cpp:582-751) -- only
    // EnterWorldService's zone-entry gate does (see EnterWorldServiceTests.
    // HandleAsync_TribeAndPreviousTribeInternallyInconsistent_AbortsSession's (Tribe:0, PreviousTribe:1) case,
    // which this exact combination would fail). Every other test in this file keeps the two fields equal (or
    // uses the documented fourth-faction exception), so none of them would catch a regression that started
    // rejecting -- or silently coercing -- a genuinely mismatched, both-main-faction pair at creation time.
    // This proves the two fields are honored independently: spawn map follows Tribe, starter kit follows
    // PreviousTribe, and the mismatch itself is not a creation-time failure.
    [Fact]
    public async Task
        HandleAsync_TribeAndPreviousTribeMismatchedWithinMainFactionRange_CreatesNormallyWithNoCrossValidation()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.RoyalSerpentKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        // Tribe 0 (Noble Dragon nation) with PreviousTribe 1 (Royal Serpent starter-kit template) -- a
        // legitimately mismatched, both-in-range pair the legacy engine never rejects at creation.
        var request = ValidRequest(12) with { Tribe = 0, PreviousTribe = 1 }; // raw code 12 -> Black Feast

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        // PreviousTribe (1) selects the starter kit; MapId (1) still follows Tribe (0) -- the two lookups are
        // independent.
        Assert.Equal(((byte)1, (short)1), starterKits.LastCall);

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)0, call!.Tribe);
        Assert.Equal((short)1, call.MapId);
        Assert.Equal((byte)1, call.PreviousTribe);
        // Royal Serpent's own weapon persisted (not Noble Dragon's), confirming the kit selection followed
        // PreviousTribe, not the mismatched Tribe.
        Assert.Contains(call.Equipment, i => i is { Slot: 7, ItemId: 85527, Enchant: 0, Combine: 0 });
    }

    [Fact]
    public async Task HandleAsync_WeaponNotOneOfTheTribesThreeAlternatives_AbortsWithoutCreating()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
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
    // PreviousTribe out of range: db-createwithstarterkit-fieldaudit finding (Major). game.Characters carries
    // CK_Characters_PreviousTribe (Migrations/018_character_previous_tribe_and_mount_readpath.sql:35-36)
    // restricting this column to 0-2 -- an out-of-range value that reached usp_Character_CreateWithStarterKit
    // would violate that constraint on the transaction's first INSERT and surface as an uncaught, uncoded
    // SqlException instead of the clean Malformed-disconnect every other bounded field on this request already
    // gets; see HandleAsync_PreviousTribeOutOfRange_AbortsAsMalformedWithoutQueryingOrCreating below for the
    // dedicated regression covering the exact values (3/255) the original audit finding exercised.
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
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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

    // db-createwithstarterkit-fieldaudit (Major), fix regression coverage. This exact scenario (PreviousTribe 3
    // or 255) previously had a green unit test here asserting a full CreateAvatarOutcome.Success response --
    // but only against FakeCharacterRepository, an in-memory stand-in with no equivalent of
    // game.Characters' CK_Characters_PreviousTribe CHECK constraint
    // (Migrations/018_character_previous_tribe_and_mount_readpath.sql:35-36). Against the real database, the
    // same out-of-range value would violate that constraint on usp_Character_CreateWithStarterKit's very first
    // INSERT, roll the whole transaction back, and surface only as an uncaught, uncoded SqlException --
    // collapsing to the same generic CreateAvatarResponse{Result=1} any other undistinguished failure
    // produces, directly contradicting what this test used to assert. CreateAvatarHandler now range-checks
    // PreviousTribe to 0-2 up front (see its own remarks for the full citation), so this input is rejected
    // here, cleanly, the same way every other structurally-invalid field on this request already is. Kept as
    // its own dedicated test (distinct from the general HandleAsync_StructuralViolation_.../-1/3 coverage
    // above) specifically to keep 255 -- the concrete value the original audit finding exercised, and the
    // one-byte wire domain's true upper bound -- under direct regression coverage.
    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public async Task HandleAsync_PreviousTribeOutOfRange_AbortsAsMalformedWithoutQueryingOrCreating(
        int previousTribe)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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

    // db-createavatar-gender-narrowing-parity finding (closing a prior round's open question, not restating
    // it). Gender was the one field among AvatarPost/Tribe/PreviousTribe/Head/Face that
    // HandleAsync_StructuralViolation_... above never covered -- it genuinely had no range check at all
    // before this fix, so an adversarial value here reached an unchecked `(byte)packet.Gender` narrowing cast
    // in HandleAsync with no rejection, silently wrapping (e.g. 256 -> 0, -1 -> 255, 1000 -> 232 via C#'s
    // unchecked int->byte truncation) into a different persisted value than legacy's own unclamped 32-bit int
    // column would have stored. Covers both directions (negative, and the first value that no longer fits a
    // byte) plus a dramatically out-of-range value a real attacker might actually send.
    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public async Task HandleAsync_GenderOutOfRange_AbortsAsMalformedWithoutQueryingOrCreating(int gender)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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

    // Companion to the out-of-range coverage above: confirms 0-255 (the byte/TINYINT storage width's own
    // natural bound, not a guessed male/female-style domain) is accepted end to end, not just "doesn't throw".
    // 255 in particular is the one-byte wire domain's true upper bound, mirroring the dedicated 255 case
    // HandleAsync_PreviousTribeOutOfRange_... above keeps under direct regression coverage for that field.
    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public async Task HandleAsync_GenderAtStorageWidthBoundary_CreatesNormallyWithExactValuePersisted(int gender)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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

    // ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:250-264: dominant-tribe gate,
    // B_CREATE_AVATAR_RECV Result=3. Tribe 0 (the requested tribe in ValidRequest) is the sole leader at the
    // 100-point floor.
    [Fact]
    public async Task HandleAsync_RequestedTribeIsDominant_RepliesResult3WithZeroedAvatarInfoAndNoPersistence()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var tribes = FakeTribeRepository.WithPoints((0, 100), (1, 0), (2, 0), (3, 0));
        var handler =
            new CreateAvatarHandler(
                new CreateAvatarService(characters, starterKits, tribes, DefaultOptions(),
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
        // Tribe 1 is dominant (150 points); ValidRequest asks for tribe 0, which this gate never touches --
        // an absolute floor on the leader's own total, not a margin/gap against the other tribes.
        var tribes = FakeTribeRepository.WithPoints((0, 10), (1, 150), (2, 0), (3, 0));
        var handler =
            new CreateAvatarHandler(
                new CreateAvatarService(characters, starterKits, tribes, DefaultOptions(),
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
                new CreateAvatarService(characters, starterKits, tribes, DefaultOptions(),
                    NullLogger<CreateAvatarService>.Instance),
                NullLogger<CreateAvatarHandler>.Instance);
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
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(), DefaultOptions(),
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

    [Fact]
    public async Task HandleAsync_TribeThree_ToggleOperatorEnabled_CreatesNormally()
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = FakeStarterKitRepository.NobleDragonKit();
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                Options.Create(new LoginServerOptions { EnableFourthFaction = true }),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        var request = ValidRequest() with { Tribe = 3 };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Equal(((byte)0, (short)140), starterKits.LastCall); // Tribe 3 -> mapcheck.h map 140

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)3, call!.Tribe);
        Assert.Equal((short)140, call.MapId);
        // Tribe (the fourth faction) and PreviousTribe (still 0, Noble Dragon) are independent fields on the
        // wire and both persisted as-is -- Behavior C's own self-consistency check (a Tribe-3/PreviousTribe-
        // 0-1-2 pairing is the one legitimate mismatch) is a zone-entry concern, not enforced here.
        Assert.Equal((byte)0, call.PreviousTribe);
        Assert.Null(session.DisconnectReason);

        var createdCharacter = await characters.GetForWorldEntryAsync(1000, CancellationToken.None);
        Assert.NotNull(createdCharacter);
        var expectedAvatarInfo = AvatarInfoFactory.CreateForCharacter(createdCharacter!) with
        {
            Vit = 1,
            Str = 1,
            Int = 1,
            Dex = 1,
            // See the equivalent block in HandleAsync_ValidRequest_RepliesResultZeroWithFullAvatarInfo above
            // for the full citation.
            Level2 = 0,
            Exp1 = 0,
            Exp2 = 0,
            StatPoint = 50,
            SkillPoint = 0,
            PreviousTribe = 0,
            Equip = AvatarInfoFactory.BuildEquipArray(call.Equipment),
            Inventory = AvatarInfoFactory.BuildInventoryArray(call.Inventory),
            Skill = AvatarInfoFactory.BuildSkillArray(call.Skills),
            HotKey = AvatarInfoFactory.BuildHotKeyArray(call.Hotkeys),
            AutoBuffTime = call.WelcomeBuffUntilDate
        };
        await PacketAssert.AssertSentAsync(pipe,
            new CreateAvatarResponse { Result = 0, AvatarInfo = expectedAvatarInfo });
    }

    // Adversarial gap fix: HandleAsync_TribeThree_ToggleOperatorEnabled_CreatesNormally above only ever
    // exercises Tribe 3 with PreviousTribe left at its request default of 0 (Noble Dragon) -- the other two
    // legitimate fourth-faction origins (PreviousTribe 1/Royal Serpent, 2/Grand Tiger -- Server/ts25zone/
    // S04_MyWork02.cpp:880-901's own "transferred in from an original tribe" case) had no coverage at all
    // proving THEIR starter kit, not Noble Dragon's, gets threaded through for a Tribe-3 character. Mirrors
    // the per-race coverage the three HandleAsync_*PreviousTribe_PersistsItsOwnStarterEquipment... tests above
    // already give the main-faction tribes.
    [Theory]
    [InlineData((byte)0, 6, 84527)] // Noble Dragon origin (raw code 6 -> Blade of the Moon)
    [InlineData((byte)1, 12, 85527)] // Royal Serpent origin (raw code 12 -> Black Feast)
    [InlineData((byte)2, 18, 86527)] // Grand Tiger origin (raw code 18 -> Qing Long's Grace)
    public async Task HandleAsync_TribeThree_EachPreviousTribeOrigin_CreatesNormallyUsingThatOriginsStarterKit(
        byte previousTribe, int weapon, int expectedWeaponItemId)
    {
        var characters = FakeCharacterRepository.WithNone();
        var starterKits = previousTribe switch
        {
            0 => FakeStarterKitRepository.NobleDragonKit(),
            1 => FakeStarterKitRepository.RoyalSerpentKit(),
            _ => FakeStarterKitRepository.GrandTigerKit()
        };
        var handler = new CreateAvatarHandler(
            new CreateAvatarService(characters, starterKits, FakeTribeRepository.Empty(),
                Options.Create(new LoginServerOptions { EnableFourthFaction = true }),
                NullLogger<CreateAvatarService>.Instance),
            NullLogger<CreateAvatarHandler>.Instance);
        var (session, _) = CreateSessionInCharSelect();

        var request = ValidRequest(weapon) with { Tribe = 3, PreviousTribe = previousTribe };

        await handler.HandleAsync(request, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        // Tribe 3 always maps to spawn map 140 regardless of which origin tribe supplied the starter kit.
        Assert.Equal((previousTribe, (short)140), starterKits.LastCall);

        var call = characters.LastCreateWithStarterKit;
        Assert.NotNull(call);
        Assert.Equal((byte)3, call!.Tribe);
        Assert.Equal((short)140, call.MapId);
        Assert.Equal(previousTribe, call.PreviousTribe);
        Assert.Contains(call.Equipment,
            i => i is { Slot: 7, Enchant: 0, Combine: 0 } && i.ItemId == expectedWeaponItemId);
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
