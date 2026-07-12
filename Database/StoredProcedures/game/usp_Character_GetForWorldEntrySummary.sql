-- Companion to usp_Character_GetForWorldEntry (5 result sets: Characters, CharacterItems, CharacterSkills,
-- CharacterHotkeys, CharacterBuffs). CharacterRepository.GetForWorldEntryAsync only ever reads RS0's first
-- 19 columns via FirstQueryAsync into CharacterWorldEntryDto and discards the rest -- but SQL Server still
-- computes and transmits all 5 result sets on every call before CaeriusNet drops them, wasted work on the
-- CreateAvatarService/ZoneTransferService/ZoneHandshakeService call paths (one round trip per zone transfer /
-- world entry / character creation, not per-tick, but not negligible either).
--
-- This procedure returns exactly those same 19 columns, in the same order, as a single result set -- no
-- CharacterQuests join, no CharacterItems/CharacterSkills/CharacterHotkeys/CharacterBuffs reads. Callers that
-- need the full bundle (items/skills/hotkeys/buffs alongside the 77-column RS0) still go through
-- usp_Character_GetForWorldEntry via GetWorldEntryBundleAsync -- this procedure is not a replacement for that
-- one, only for the narrow single-result-set read.
CREATE PROCEDURE game.usp_Character_GetForWorldEntrySummary @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CharacterId,
           c.AccountId,
           c.Slot,
           c.Name,
           c.Tribe,
           c.Gender,
           c.HeadType,
           c.FaceType,
           c.Level,
           c.MapId,
           c.PosX,
           c.PosY,
           c.PosZ,
           c.Heading,
           c.Life,
           c.MaxLife,
           c.Mana,
           c.MaxMana,
           c.FlushSequence
    FROM game.Characters AS c
    WHERE c.CharacterId = @CharacterId;
END;
