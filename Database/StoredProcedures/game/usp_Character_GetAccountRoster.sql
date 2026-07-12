-- Account-login avatar roster population fix (legacy-behavior-translator contract "Account-login avatar
-- roster population", CL_LOGIN_SEND / LC_USER_AVATAR_RECV2): usp_Character_GetByAccount (already applied,
-- never edited in place -- see Database/_manifest.txt's own header rule) only ever selected the 8-column
-- character-select summary; the roster response the client actually renders on every account login has
-- always shown zeroed equipment/inventory/stats for a returning character because nothing downstream of
-- that procedure ever read the rest of the persisted row. This is a companion READ-ONLY procedure, not a
-- replacement -- GetByAccountAsync/CharacterSummaryDto keep working exactly as before for every existing
-- caller (CreateAvatarService's slot-occupancy check, DeleteAvatarService's slot resolution).
--
-- RS0: one row per occupied roster slot (up to 3, MAX_USER_AVATAR_NUM) with every scalar the roster response
-- actually carries, beyond CharacterSummaryDto's existing narrow set. Deliberately does NOT select
-- StatVit/StatStr/StatInt/StatDex/StatPoints/Money/BigMoney/StoreMoney/BigStoreMoney/Experience/Exp2/quest
-- state/skills/hotkeys/buffs -- the roster response never sends any of those (see the contract's own
-- "Fields never present on this response" list); selecting them here would misrepresent this procedure's
-- actual wire contract.
--
-- RS1: every occupied game.CharacterItems row (Equipment=2, Inventory=0/1, Store=3/4) for every character on
-- this account in ONE round trip, tagged with CharacterId so the caller can split them back out per slot --
-- avoids an N+1 per-character items query for a 3-slot roster. Column shape mirrors
-- usp_Character_GetForWorldEntry's own RS1 (CharacterItemSlotDto), with CharacterId prepended since this
-- read spans multiple characters where that one covers exactly one.
--
-- Deliberately NOT sourced here (left to the caller, same composition EnterWorldService.HandleAsync already
-- uses for the single-character world-entry path -- see that method's own guildTask/friendsTask/mentorTask
-- calls): GuildName (game.usp_GuildMember_GetByCharacter / IGuildRepository.GetByCharacterAsync), Friend
-- names (game.usp_CharacterFriend_GetByCharacter / IFriendRepository.GetByCharacterAsync), Teacher/Student
-- names (game.usp_CharacterMentor_GetForCharacter / IMentorRepository.GetForCharacterAsync). Duplicating
-- those joins in this procedure too would create a second, divergent source of truth for the exact same
-- data those repositories already resolve live.
--
-- VisibleState/SpecialState/CostumeIndex (game.Characters) and PetBag/Costume (game.CharacterPetBag/
-- game.CharacterCostumes, RS2/RS3 below) close the account-login avatar roster reconstruction gap this
-- procedure's header used to flag as unsourced -- see game.Characters.sql's own column comments for
-- VisibleState/SpecialState/CostumeIndex and each child table's own header for PetBag/Costume.
--
-- Also NOT applied here (in-memory transformations the contract documents as happening on top of a raw read,
-- not as part of it -- see that contract's own "Side effects" section): the logout-position tribe-consistency
-- correction (LogoutInfo[0..3] replaced with a tribe-specific town-spawn set when the persisted MapId isn't
-- the character's own tribe's) and the item ids 1451/2268 blacklist purge (Fenrir.Application.Login.Domain.
-- Avatars.AvatarInfoFactory.IsLoginRosterBlacklistedItemId, applied in BuildEquipArrayFromRosterItems/
-- BuildInventoryArrayFromRosterItems/BuildStoreItemArrayFromRosterItems). Both are call-site business logic,
-- not a data-access concern -- MapId/PosX/PosY/PosZ/Life/Mana and every raw ItemId are returned unfiltered in
-- RS0/RS1 for the caller to apply both transforms to.
CREATE PROCEDURE game.usp_Character_GetAccountRoster @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CharacterId,
           Slot,
           Name,
           Tribe,
           PreviousTribe,
           Gender,
           HeadType,
           FaceType,
           Level,
           Level2,
           Halo,
           RebirthCount,
           ContributionPoints,
           SkillPoints,
           EatLifePotion,
           EatManaPotion,
           EatStrPotion,
           EatDexPotion,
           EatElePotion,
           PetGrowth,
           PetActivity,
           MapId,
           PosX,
           PosY,
           PosZ,
           Life,
           Mana,
           VisibleState,
           SpecialState,
           CostumeIndex
    FROM game.Characters
    WHERE AccountId = @AccountId
    ORDER BY Slot;

    SELECT ci.CharacterId,
           ci.Container,
           ci.Slot,
           ci.ItemId,
           CAST(ci.Quantity AS INT) AS Quantity, -- ci.Quantity is SMALLINT; widen back to INT here so
           ci.Enchant,                           -- CharacterRosterItemDto's existing int-typed ctor param
           ci.Combine,                           -- keeps reading it via SqlDataReader.GetInt32 without an
           ci.Refine,                            -- InvalidCastException (see CharacterItems.sql's own comment)
           ci.Socket,
           ci.SocketGem1,
           ci.SocketGem2,
           ci.SocketGem3,
           ci.ExpireDate,
           ci.Serial
    FROM game.CharacterItems AS ci
             JOIN game.Characters AS c ON c.CharacterId = ci.CharacterId
    WHERE c.AccountId = @AccountId
    ORDER BY ci.CharacterId, ci.Container, ci.Slot;

    SELECT pb.CharacterId,
           pb.Slot,
           pb.ItemId
    FROM game.CharacterPetBag AS pb
             JOIN game.Characters AS c ON c.CharacterId = pb.CharacterId
    WHERE c.AccountId = @AccountId
    ORDER BY pb.CharacterId, pb.Slot;

    SELECT cc.CharacterId,
           cc.Slot,
           cc.ItemId
    FROM game.CharacterCostumes AS cc
             JOIN game.Characters AS c ON c.CharacterId = cc.CharacterId
    WHERE c.AccountId = @AccountId
    ORDER BY cc.CharacterId, cc.Slot;
END;
