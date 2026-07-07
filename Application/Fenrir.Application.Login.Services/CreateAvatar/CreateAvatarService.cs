using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.CreateAvatar;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 business logic -- creates a new character in the requested slot, grants the EU33
///     starter kit (tribe equipment/inventory/skills/hotkeys, stats, pet, welcome buffs, one premium day) and builds
///     the full AVATAR_INFO payload.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-1183 -- USE_CUSTOME_CREATE branch (see
///     Migrations/015_starter_kit_elite_grant.sql's header comment for the full citation: this macro is
///     force-defined, unguarded, at S04_MyWork02.cpp:1, before any other header, bypassing the M33/LNW33EU
///     build-variant chain every OTHER file in the codebase is subject to -- so the `#else`/`#ifdef` arms
///     below are what actually ships in every build configuration, not the `#ifndef` arm a prior version of
///     this file assumed) ; Server/Header/mapcheck.h:298-326 (GetReturnBornInTownLocation) ;
///     Server/ts25login/S04_MyWork02.cpp:625-635 (the combined slot-occupied/name-empty test -- name-empty is
///     handled upstream by CreateAvatarHandler, slot-occupancy is checked here) ;
///     Server/ts25login/S04_MyWork02.cpp:635-646 (the fourth-faction/Tribe-value-3 creation exclusion -- see
///     <see cref="FourthFactionGate" /> for the gate itself and <see cref="LoginServerOptions.EnableFourthFaction" />
///     for the operator toggle) ; Server/ts25login/S04_MyWork02.cpp:739-838 (the PreviousTribe/race switch has no
///     case-3/default branch -- weapon validation only runs for PreviousTribe 0/1/2, see
///     <see cref="TryResolveWeaponItemId" />'s call site) ; Server/ts25login/S04_MyWork02.cpp:1100-1179 (starting
///     level/stats/skill points, the six-category Enchant/Combine grant, and the pet/cape/mount grant -- all
///     literal legacy constants embedded directly in usp_Character_CreateWithStarterKit, not parameters here) ;
///     Server/ts25login/S04_MyWork02.cpp:1100-1110 specifically (<see cref="StartingLevel2" />/
///     <see cref="StartingExp2" />/<see cref="StartingStatPoint" />/<see cref="StartingSkillPoint" /> -- these
///     four are now also mirrored onto the create-avatar confirmation response itself, not just persisted:
///     previously the response's narrow CharacterWorldEntryDto-backed overlay omitted them entirely, so the
///     client was shown Level2/Exp2/StatPoint/SkillPoint = 0 immediately after creation even though the
///     database correctly recorded 12/0/3175/10000 -- a display-only mismatch confined to this one response,
///     self-correcting the next time the character actually enters the world via Game's own
///     AvatarInfoFactory.CreateForCharacter reading the fuller CharacterWorldSnapshotDto) ;
///     Server/ts25login/S04_MyWork02.cpp:1105 (<see cref="StartingExp1" /> -- aExp1 = MAX_NUMBER_SIZE) ;
///     Server/Header/Protocol/DEFINE.h:365 (MAX_NUMBER_SIZE = 2000000000): fixed here at creation
///     (create-avatar-stats-fresh finding, severity blocker, previously showing 0 XP permanently regardless
///     of actual progress). The broader "not just creation, EVERY subsequent world entry too" variant
///     (zone-avatarinfo-factory-parity, severity Major) is now also resolved, NOT left unset as a prior
///     revision of this comment said: aExp1 is a fixed constant (MAX_NUMBER_SIZE) for every Fenrir
///     character's entire lifetime, not a value requiring decomposition from the combined Experience
///     counter, because every character is created already at the general-level cap and
///     MyUtil::ProcessForExperience never reassigns aExp1 once it already equals MAX_NUMBER_SIZE
///     (Server/ts25zone/S07_MyGame03.cpp:196-198,341) -- see
///     <c>Fenrir.Application.Game.Domain.Avatars.AvatarInfoFactory.MaxGeneralExperience</c>'s own remarks
///     (consumed by both <c>CreateForCharacter</c> and <c>CreateForRuntimeState</c>) for the full citation
///     chain.
///     Server/ts25login/S04_MyWork02.cpp:1094-1097 (the current-life/current-mana literals, 30/21, and the
///     DEFINE.h:751-756 write-macro confirming those are the logout-info slots this session's own current
///     values, not any maximum) ; Server/ts25login/S04_MyWork02.cpp:1174-1179 (the universal starter mount --
///     item 1301/DEFINE.h:157 ANIMAL_NUM_TIGER1, "5%" power, slot 0, permanent expiry) ;
///     Server/ts25login/S04_MyWork02.cpp:885-892 (DoubleExpTime1/DoubleExpTime2 vs AutoBuffTime are two
///     independently-sourced legacy fields -- DoubleExpTime1/2 a raw 300-tick counter, AutoBuffTime a
///     genuine 7-day-future date -- confirmed by Server/ts25zone/S07_MyGame04.cpp:955-969's decrement-on-tick
///     consumer, which proves the two are never compared against a calendar date; see
///     <see cref="DoubleExpCounterStart" /> for the C# fix and
///     Migrations/026_double_exp_time_counter_fix.sql for the stored-procedure side) ;
///     Server/ts25zone/S04_MyWork02.cpp:880-901 (the Tribe/PreviousTribe self-consistency check zone entry
///     performs against the persisted PreviousTribe this method now threads through to
///     <see cref="ICharacterRepository.CreateWithStarterKitAsync" /> instead of leaving it un-persisted).
/// </remarks>
public sealed class CreateAvatarService(
    ICharacterRepository characters,
    IStarterKitRepository starterKits,
    ITribeRepository tribes,
    IOptions<LoginServerOptions> options,
    ILogger<CreateAvatarService> logger)
    : ICreateAvatarService
{
    // S04_MyWork02.cpp:1096-1097; DEFINE.h:751-756's write-macro confirms these logout-info slots are
    // current life/current mana. Universal across every race/tribe/gender -- these are deliberately low
    // relative to a near-max-level, fully-elite-geared character's real maximum (the character is topped
    // up, or its true maximums recomputed, on first entry into the world), not a mistranscribed number.
    private const int StartLife = 30;
    private const int StartMana = 21;

    // MaxLife/MaxMana have no creation-time legacy value at all -- the legacy engine never persists a
    // maximum, it recomputes both dynamically every time a relevant input changes, from a multi-factor
    // formula (vitality/ki, gear, elixir, level, mount tier, pet, item-set bonuses --
    // Server/Header/Protocol/MyFactor.cpp:1902-2010,2226-2357). These two constants are therefore a known
    // architecture-mismatch placeholder, not a legacy-cited value; treat neither number as authoritative --
    // a dedicated MyFactor-formula contract is needed before any concrete number or persistence strategy is
    // chosen (bridge-stats-equipment contract, gap-table row 11).
    private const int StartMaxLife = 100;
    private const int StartMaxMana = 50;

    // FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h): the slots BuildEquipmentRows writes beyond the tribe catalog.
    private const byte CapeEquipSlot = 1;
    private const byte WeaponEquipSlot = 7;
    private const byte PetEquipSlot = 8;

    private const int StarterCapeItemId = 1407; // Flying Wizard, 120% (legacy: SetISIUIMValue(40,0,0,0))
    private const byte StarterCapeEnchant = 40;
    private const int StarterPetItemId = 2300; // Yin Yang Free; PetGrowth/PetActivity live on game.Characters itself

    // Server/ts25login/S04_MyWork02.cpp:1131-1135: tAvatarInfo.aEquip[EPET][1] = MAX_PAT_ACTIVITY_SIZE (full
    // activity), aEquip[EPET][2] = 640,000,000 (200% growth, a raw value -- not a packed Enchant/Combine/
    // Refine/Socket byte quad). Identical literals to the ones this method's own CreateWithStarterKitAsync
    // call persists onto game.Characters.PetActivity/PetGrowth (Migrations/016_starter_kit_create_atomicity.sql:77),
    // overlaid onto the immediate response the same "known at creation time, not read back" way
    // StarterMountItemId/StartingLevel2 are above -- closes the zone-avatarinfo-factory-parity finding
    // (starter pet always displaying 0% growth/0 activity) for the create-avatar response itself.
    private const int StarterPetGrowth = 640_000_000;
    private const byte StarterPetActivity = 100;

    // Server/ts25login/S04_MyWork02.cpp:1112-1120: SetISIUIMValue(45, 6, 0, 0) on the six elite-gear
    // categories (Weapon/Armor/Gloves/Ring/Boots/Amulet, i.e. every StarterKitEquipmentRowDto row) -- despite
    // the adjacent "Enchant[20%]" comment at line 1112, the literal first argument is 45, not 20.
    private const byte EliteGearEnchant = 45;
    private const byte EliteGearCombine = 6;

    // Server/ts25login/S04_MyWork02.cpp:1174-1179 (item id: DEFINE.h:157 ANIMAL_NUM_TIGER1): the universal
    // starter mount -- one tiger mount, slot 0, "5%" power tier, already select/equipped, effectively-
    // permanent expiry. Identical to the literals 016_starter_kit_create_atomicity.sql bakes into
    // MountItemId/MountExpActivity/MountPower/MountSlotIndex/MountTime -- game.Characters.Mount* isn't
    // projected onto CharacterWorldEntryDto (see its own doc comment), so this is a second independent
    // literal overlaid onto the immediate response, the same "known at creation time, not read back"
    // pattern as Vit/Str/Int/Dex below.
    private const int StarterMountItemId = 1301;
    private const int StarterMountExpActivity = 0;
    private const int StarterMountPower = 5;
    private const int StarterMountSlotIndex = 0;
    private const int StarterMountTime = 99999999;

    // AutoBuffTime is genuinely this many days out (S04_MyWork02.cpp:892, "Starting Auto Buff Scroll: 7
    // days") -- confirmed legacy-accurate.
    private const int WelcomeBuffDurationDays = 7;

    // Server/ts25login/S04_MyWork02.cpp:886-887: aDoubleExpTime1/aDoubleExpTime2 are bare integer literals
    // (300), NOT date-derived -- Server/ts25zone/S07_MyGame04.cpp:955-969's periodic per-tick handler proves
    // these are raw decrement-if-greater-than-zero counters (never compared against a YYYYMMDD date
    // anywhere), wholly independent of AutoBuffTime's genuine 7-day-future date above. Previously collapsed
    // onto the same date value as AutoBuffTime -- a confirmed-wrong ~20-million-fold inflation of the
    // welcome-buff duration (a date like 20260714 fed into decrement-by-1-per-tick logic instead of 300).
    // Now persisted as this literal instead, matching usp_Character_CreateWithStarterKit's own hardcoded
    // 300/300 (Migrations/026_double_exp_time_counter_fix.sql) -- the same "literal baked directly into the
    // stored procedure" treatment already used for the starting level/stat/pet/mount grants there. The
    // real-world wall-clock duration one such tick represents, and the runtime decrement/notify consumer
    // itself, are a separate, not-yet-implemented Game-side concern (see the behavior contract's own "open
    // question" on the tick-to-wall-clock rate) -- out of scope for this creation-time fix.
    private const int DoubleExpCounterStart = 300;

    // Server/ts25login/S04_MyWork02.cpp:889: aProtectForDeath = 5, a bare integer literal in the same live
    // #ifdef LNW33 block as DoubleExpCounterStart/StarterAutoTime2Minutes above/below -- the starting
    // death-protection allowance (qualifying deaths the character may suffer before the normal XP/
    // contribution-point death penalty resumes applying, Server/ts25zone/S07_MyGame02.cpp:3443-3489).
    // Migrations/025_character_protect_for_death_grant.sql already persists this same literal into
    // game.Characters.ProtectForDeath; that migration's own text deferred whether the value is additionally
    // echoed back to the client on this response as a separate open question -- closed here for the
    // creation-response path only (S04_MyWork02.cpp:582-605/1181-1182 establish the same local AVATAR_INFO
    // instance that receives this grant is the one passed to the create-avatar response transfer call).
    // "Known at creation time, not read back" overlay, same posture as StarterAutoTime2Minutes/
    // DoubleExpCounterStart: CharacterWorldEntryDto doesn't project ProtectForDeath at all (see its own doc
    // comment -- it's a deliberately narrow DTO), so this literal is used directly rather than a second round
    // trip. The world-entry (Game's AvatarInfoFactory.CreateForCharacter) and zone-to-zone-transfer
    // (CreateForRuntimeState) projections are NOT touched by this fix -- both remain open questions
    // (unverified ts25zone world-entry citation, and a missing PlayerRuntimeState field respectively), see
    // each factory method's own remarks.
    private const int StartingProtectForDeath = 5;

    // Server/ts25login/S04_MyWork02.cpp:888: aAutoTime2 = 1440, a bare integer literal in the same live
    // #ifdef LNW33 block as DoubleExpCounterStart/ProtectForDeath above -- the free auto-hunt time allowance
    // granted at creation. Confirmed by direct legacy re-read (this session, closing the prior audit round's
    // open "aAutoTime2 completely unrepresented" question): Server/ts25zone/S07_MyGame04.cpp:787-823 (itself
    // under the always-compiled `#ifndef FREE_HUNT` -- FREE_HUNT is declared only in DEFINE.h's never-taken
    // `#else` arm of `#ifdef M33`, the same arm that would otherwise skip LNW33 too) decrements this counter
    // by exactly 1 per elapsed real minute (GetMinuteFromTick(1) == TICK_MINUTE == 60000ms, Server/Header/
    // datetime.h:29,33) while auto-hunt is active, so 1440 == 24h of free auto-hunt minutes -- corroborated by
    // the "auto hunt 5hr"/"3hr" cash items (Server/ts25zone/S04_MyWork03.cpp:5387-5407) adding 300/180 minutes
    // respectively. See Migrations/027_character_autotime2_grant.sql for the stored-procedure side.
    private const int StarterAutoTime2Minutes = 1440;

    private const int PremiumDurationDays = 1;

    // Server/ts25login/S04_MyWork02.cpp:1100-1110 (USE_CUSTOME_CREATE block): the high-level/rebirth-ladder
    // level, its own experience counter, and the two point pools are fixed literal constants applied
    // unconditionally at creation, identical for every tribe/gender/previous-tribe -- already baked into
    // usp_Character_CreateWithStarterKit's own INSERT
    // (Migrations/018_character_previous_tribe_and_mount_readpath.sql:113) the same way DoubleExpCounterStart/
    // StarterMountItemId above are. CharacterWorldEntryDto's narrow read doesn't carry any of these four
    // columns back (see its own doc comment), so -- exactly like Vit/Str/Int/Dex below -- the response overlay
    // uses these C# literals directly instead of a second round trip. Previously omitted from the overlay
    // entirely, which left the create-avatar confirmation response showing all four at their zero/default
    // template value even though the character record itself was persisted correctly (create-avatar-stats-
    // fresh finding).
    private const int StartingLevel2 = 12;
    private const int StartingExp2 = 0;
    private const int StartingStatPoint = 3175;
    private const int StartingSkillPoint = 10000;

    // Server/ts25login/S04_MyWork02.cpp:1105 (tAvatarInfo.aExp1 = MAX_NUMBER_SIZE) ; Server/Header/Protocol/
    // DEFINE.h:365 (#define MAX_NUMBER_SIZE 2000000000). Same "known at creation time, not read back" overlay
    // as StartingLevel2/StartingExp2 above -- usp_Character_CreateWithStarterKit already persists this exact
    // literal into game.Characters.Experience (Migrations/018_character_previous_tribe_and_mount_readpath.sql:113),
    // but CharacterWorldEntryDto doesn't carry that column back (see its own doc comment), so this was
    // previously left at Zeroed's 0 default on the create-avatar response itself (create-avatar-stats-fresh
    // finding). Unlike StartingLevel2/StartingExp2/StartingStatPoint/StartingSkillPoint, this one does NOT
    // self-correct on the next zone entry -- but what SHOULD replace the 0 on that next zone entry is its own
    // separate open question (zone-avatarinfo-factory-parity finding, Major), deliberately NOT resolved here:
    // see AvatarInfoFactory (Game)'s own Exp1 remarks on both CreateForCharacter/CreateForRuntimeState for why
    // that side is intentionally left unpopulated pending legacy re-verification, rather than guessed.
    private const int StartingExp1 = 2_000_000_000;

    // GetReturnBornInTownLocation: Tribe (the playable faction, 0-3) decides the spawn map. PreviousTribe (the
    // Noble Dragon/Royal Serpent/Grand Tiger starting-kit template, 0-2) separately decides equipment/inventory/
    // skills/hotkeys below -- the two fields are independent on the wire and both are honored independently here.
    private static readonly short[] SpawnMapIdByTribe = [1, 6, 11, 140];

    public async ValueTask<CreateAvatarResult> CreateAvatarAsync(
        int accountId,
        byte avatarPost,
        string avatarName,
        byte tribe,
        byte previousTribe,
        byte gender,
        byte head,
        byte face,
        int weapon,
        CancellationToken cancellationToken)
    {
        // Legacy order (S04_MyWork02.cpp): the combined slot-occupied/name-empty test (625-635) runs before the
        // fourth-faction exclusion (640-646), which in turn runs before the dominant-tribe gate (724-730) --
        // checked in that same order here. Only slot-occupancy is checked below; the request's name is already
        // known non-empty by this point (CreateAvatarHandler rejects an empty name before this method runs).
        var existingCharacters = await characters.GetByAccountAsync(accountId, cancellationToken);
        if (existingCharacters.Any(character => character.Slot == avatarPost))
            return new CreateAvatarResult(CreateAvatarOutcome.SlotOccupied, AvatarInfoFactory.Zeroed);

        if (FourthFactionGate.BlocksCreation(tribe, options.Value.EnableFourthFaction))
            return new CreateAvatarResult(CreateAvatarOutcome.FourthFactionDisabled, AvatarInfoFactory.Zeroed);

        // Cross-executable read path: Login has no shared-memory segment with Game (unlike the legacy
        // single-process design -- see TribeDominanceGate's <remarks>), so this reads game.WorldStateTribes.Points
        // through the same ITribeRepository/usp_Tribe_GetAll surface Game's own tribe features already use --
        // no new table, no new schema. Game's WorldStateService caches tribe points in memory and only persists
        // them via a write-behind flush every 5s (Fenrir.Application.Game.Hosting.World.WorldState.
        // WorldStateWriteBehindHost.Interval), so this read can lag the true in-memory standings by up to that
        // interval; the legacy shared-memory read had no such delay. Accepted here as a bounded, documented
        // deviation rather than adding a synchronous cross-process call on the avatar-creation path.
        if (TribeDominanceGate.BlocksCreation(tribe, await tribes.GetAllAsync(cancellationToken)))
            return new CreateAvatarResult(CreateAvatarOutcome.DominantTribeBlocked, AvatarInfoFactory.Zeroed);

        var mapId = SpawnMapIdByTribe[tribe];

        var kit = await starterKits.GetByPreviousTribeAsync(previousTribe, mapId, cancellationToken);

        // The weapon-vs-race matching switch (S04_MyWork02.cpp:739-838) only has cases for PreviousTribe 0/1/2
        // (Noble Dragon/Royal Serpent/Grand Tiger) and no case-3/default branch -- legacy itself grants nothing
        // for any other value rather than rejecting the request on this basis. CreateAvatarHandler now
        // range-checks PreviousTribe to 0-2 before ever calling this method (see its own remarks for the full
        // citation and the CK_Characters_PreviousTribe DB constraint that motivated it), so in practice
        // previousTribe is always 0/1/2 by the time this line runs; the `is 0 or 1 or 2` guard below is kept as
        // defense-in-depth for any other caller of this public service method, not because an out-of-range
        // value is still expected to reach here from the handler.
        var weaponItemId = 0;
        if (previousTribe is 0 or 1 or 2 && !TryResolveWeaponItemId(kit.Equipment, weapon, out weaponItemId))
            return new CreateAvatarResult(CreateAvatarOutcome.InvalidWeapon, AvatarInfoFactory.Zeroed);

        var equipment = BuildEquipmentRows(kit.Equipment, weaponItemId);
        var inventory = BuildInventoryRows(kit.Inventory);
        var skills = BuildSkillRows(kit.Skills);
        var hotkeys = BuildHotkeyRows(kit.Hotkeys);

        var welcomeBuffUntilDate = TodayPlusDays(WelcomeBuffDurationDays);
        var premiumUntilUnixSeconds = DateTimeOffset.UtcNow.AddDays(PremiumDurationDays).ToUnixTimeSeconds();

        try
        {
            var characterId = await characters.CreateWithStarterKitAsync(
                accountId,
                avatarPost,
                avatarName,
                tribe,
                gender,
                head,
                face,
                mapId,
                kit.Spawn?.PosX ?? 0f,
                kit.Spawn?.PosY ?? 0f,
                kit.Spawn?.PosZ ?? 0f,
                StartLife,
                StartMaxLife,
                StartMana,
                StartMaxMana,
                welcomeBuffUntilDate,
                premiumUntilUnixSeconds,
                equipment,
                inventory,
                skills,
                hotkeys,
                cancellationToken,
                previousTribe);

            // Guaranteed non-null: we just created this row within this request.
            var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);

            // CharacterWorldEntryDto is a stable narrow prefix (see its own doc comment) that doesn't carry
            // stats/equipment/buffs/premium/PreviousTribe/Mount* -- overlaid here instead of added to that DTO,
            // since every one of these is already known without a second round trip.
            var avatarInfo = AvatarInfoFactory.CreateForCharacter(character!) with
            {
                // Independent fixed literal constants (S04_MyWork02.cpp:748-751), applied unconditionally
                // before the race switch -- NOT read back from the row above, they simply equal the same
                // literals CreateWithStarterKitAsync also persisted.
                Vit = 1,
                Str = 1,
                Int = 1,
                Dex = 1,
                // Fixed literal constants applied unconditionally at creation (see StartingLevel2's own
                // remarks for the full citation) -- NOT read back from the row above, for the same reason
                // Vit/Str/Int/Dex above aren't.
                Level2 = StartingLevel2,
                // aExp1 sentinel (see StartingExp1's own remarks for the full citation) -- closes the
                // create-avatar-stats-fresh finding's blocker on this response.
                Exp1 = StartingExp1,
                Exp2 = StartingExp2,
                StatPoint = StartingStatPoint,
                SkillPoint = StartingSkillPoint,
                // game.Characters.PreviousTribe (Migrations/018_character_previous_tribe_and_mount_readpath.sql)
                // isn't projected onto CharacterWorldEntryDto either -- already known here as the request's
                // own (unvalidated-by-design, see this method's own remarks) parameter.
                PreviousTribe = previousTribe,
                Equip = AvatarInfoFactory.BuildEquipArray(equipment, StarterPetGrowth, StarterPetActivity),
                // zone-avatarinfo-factory-parity finding: Inventory/Skill/HotKey were previously left at
                // Zeroed's all-zero default even though this method already builds the exact TVP rows it's
                // about to persist -- StoreItem stays at 0 deliberately (no starter kit ever grants a
                // warehouse/Store-container row, see AvatarInfoFactory.BuildInventoryArray's own remarks).
                Inventory = AvatarInfoFactory.BuildInventoryArray(inventory),
                Skill = AvatarInfoFactory.BuildSkillArray(skills),
                HotKey = AvatarInfoFactory.BuildHotKeyArray(hotkeys),
                Animal = SingleMountSlotArray(StarterMountItemId),
                AnimalIndex = StarterMountSlotIndex,
                AnimalTime = StarterMountTime,
                AnimalPower = SingleMountSlotArray(StarterMountPower),
                AnimalExpActivity = SingleMountSlotArray(StarterMountExpActivity),
                // Closes the creation-response slice of the ProtectForDeath display gap (see
                // StartingProtectForDeath's own remarks for the full citation) -- usp_Character_CreateWithStarterKit
                // already bakes 5 in directly for this column (Migrations/025_character_protect_for_death_grant.sql),
                // so the response overlay uses the same C# constant rather than re-deriving it, exactly like
                // AutoTime2/DoubleExpTime1/DoubleExpTime2 below.
                ProtectForDeath = StartingProtectForDeath,
                // DoubleExpTime1/DoubleExpTime2 are the raw 300-tick counter (see DoubleExpCounterStart's own
                // remarks for the full citation) -- genuinely independent of AutoBuffTime's 7-day-future date,
                // not the same value collapsed onto all three fields. usp_Character_CreateWithStarterKit now
                // bakes 300/300 in directly for these two columns (Migrations/026_double_exp_time_counter_fix.sql),
                // so the response overlay uses the same C# constant rather than re-deriving it, exactly like
                // StarterMountItemId/StarterMountPower above.
                DoubleExpTime1 = DoubleExpCounterStart,
                DoubleExpTime2 = DoubleExpCounterStart,
                AutoBuffTime = welcomeBuffUntilDate,
                // Closes this session's own aAutoTime2 finding (see StarterAutoTime2Minutes' own remarks for
                // the full citation) -- usp_Character_CreateWithStarterKit now bakes 1440 in directly for this
                // column (Migrations/027_character_autotime2_grant.sql), so the response overlay uses the same
                // C# constant rather than re-deriving it, exactly like DoubleExpTime1/DoubleExpTime2 above.
                AutoTime2 = StarterAutoTime2Minutes,
                Premium = premiumUntilUnixSeconds
            };

            return new CreateAvatarResult(CreateAvatarOutcome.Success, avatarInfo);
        }
        catch (Exception ex)
        {
            // usp_Character_CreateWithStarterKit throws distinct codes (slot occupied/name taken), but the wire
            // contract only documents Result=1 for any failure -- the legacy client has no finer-grained handling.
            // Previously swallowed with no trace at all; logged here (the only place the exception itself is
            // still in scope) so a real starter-kit failure is diagnosable instead of vanishing silently.
            logger.LogError(ex,
                "Character creation failed for account {AccountId} slot {AvatarPost} name {AvatarName}",
                accountId, avatarPost, avatarName);
            return new CreateAvatarResult(CreateAvatarOutcome.Failure, AvatarInfoFactory.Zeroed);
        }
    }

    /// <summary>
    ///     The client's raw weapon code must match one of the tribe's 3 catalogued RawWeaponCode values
    ///     (EquipSlot 7 rows) -- anything else is the same "not one of my 3 offered weapons" case the legacy
    ///     Quit()s on. The returned id is the row's ItemId, i.e. the elite weapon the raw code remaps to
    ///     (Server/ts25login/S04_MyWork02.cpp:773-778/801-806/829-834's `if (tWeapon == N) tWeapon =
    ///     <elite id>
    ///         `),
    ///         never the raw code itself.
    /// </summary>
    private static bool TryResolveWeaponItemId(IReadOnlyList<StarterKitEquipmentRowDto> equipment,
        int requestedWeapon, out int weaponItemId)
    {
        foreach (var row in equipment)
            if (row.EquipSlot == WeaponEquipSlot && row.RawWeaponCode == requestedWeapon)
            {
                weaponItemId = row.ItemId;
                return true;
            }

        weaponItemId = 0;
        return false;
    }

    /// <summary>
    ///     Amulet/Armor/Gloves/Ring/Boots + the one chosen Weapon from the tribe's elite catalog (each granted
    ///     with the six-category Enchant/Combine encoding, see <see cref="EliteGearEnchant" />), plus the
    ///     universal Cape/Pet.
    /// </summary>
    private static List<CharacterItemSlotTvp> BuildEquipmentRows(IReadOnlyList<StarterKitEquipmentRowDto> catalog,
        int weaponItemId)
    {
        var rows = new List<CharacterItemSlotTvp>(catalog.Count + 2);

        foreach (var row in catalog)
        {
            if (row.EquipSlot == WeaponEquipSlot && row.ItemId != weaponItemId)
                continue; // the 2 un-chosen weapon alternatives

            rows.Add(new CharacterItemSlotTvp(row.EquipSlot, row.ItemId, 1, EliteGearEnchant, EliteGearCombine, 0, 0,
                0, 0, 0, 0, 0));
        }

        rows.Add(new CharacterItemSlotTvp(CapeEquipSlot, StarterCapeItemId, 1, StarterCapeEnchant, 0, 0, 0, 0, 0, 0, 0,
            0));
        rows.Add(new CharacterItemSlotTvp(PetEquipSlot, StarterPetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        return rows;
    }

    private static List<CharacterItemSlotTvp> BuildInventoryRows(IReadOnlyList<StarterKitInventoryRowDto> catalog)
    {
        var rows = new List<CharacterItemSlotTvp>(catalog.Count);

        foreach (var row in catalog)
            rows.Add(new CharacterItemSlotTvp(row.SlotIndex, row.ItemId, row.Quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        return rows;
    }

    private static List<CharacterSkillSlotTvp> BuildSkillRows(IReadOnlyList<StarterKitSkillRowDto> catalog)
    {
        var rows = new List<CharacterSkillSlotTvp>(catalog.Count);

        foreach (var row in catalog)
            rows.Add(new CharacterSkillSlotTvp(row.SlotIndex, row.SkillId, row.Grade));

        return rows;
    }

    private static List<CharacterHotkeySlotTvp> BuildHotkeyRows(IReadOnlyList<StarterKitHotkeyRowDto> catalog)
    {
        var rows = new List<CharacterHotkeySlotTvp>(catalog.Count);

        foreach (var row in catalog)
            rows.Add(new CharacterHotkeySlotTvp(row.Page, row.KeyIndex, row.Sort, row.Value1, row.Value2));

        return rows;
    }

    /// <summary>
    ///     Legacy datetime.h::ReturnAddDate(0, days) -- YYYYMMDD, the same encoding Game's own GameDate.Today() uses
    ///     (Login has no reference to the Game project, so this is a deliberately independent one-liner, not a
    ///     copy-paste).
    /// </summary>
    private static int TodayPlusDays(int days)
    {
        var future = DateTime.UtcNow.AddDays(days);
        return future.Year * 10000 + future.Month * 100 + future.Day;
    }

    /// <summary>
    ///     Places a single value at <see cref="StarterMountSlotIndex" /> of an otherwise-zeroed 10-slot array --
    ///     the shared shape of AVATAR_INFO's Animal/AnimalPower/AnimalExpActivity arrays, all of which the wire
    ///     format sizes at 10 possible owned-mount slots even though creation only ever grants one.
    /// </summary>
    private static int[] SingleMountSlotArray(int value)
    {
        var slots = new int[10];
        slots[StarterMountSlotIndex] = value;
        return slots;
    }
}
