-- auto-hunt-item-consumption: single-slot upsert/clear for game.CharacterHotkeys, backing op22
-- CZ_USE_HOTKEY_ITEM_SEND's hotkey-bound potion/consumable activation
-- (HotkeyItemConsumptionResolver) -- previously the table had no writer beyond the one-time starter-kit
-- grant. Mirrors usp_CharacterSkills_UpsertSlot's own DELETE-then-conditionally-INSERT shape.
--
-- game.CharacterHotkeys stores the legacy raw triple verbatim as (Sort, Value1, Value2) -- per that table's
-- own DDL comment, the FIRST raw int is the bound id (skill id or item id), the SECOND is the secondary
-- value (grade or quantity), and the THIRD is the actual HOTKEY_SORT/kind discriminator, NOT the first int
-- despite the "Sort" column name. @Value2 (the kind discriminator) of 0 means the slot is now empty
-- (HotkeyBindingKind.None) -- the row is deleted and never re-inserted, matching "row absence = unassigned
-- key" (PlayerRuntimeState.SetHotkeySlot's own convention).
CREATE PROCEDURE game.usp_CharacterHotkeys_UpsertSlot @CharacterId INT,
                                                      @Page TINYINT,
                                                      @KeyIndex TINYINT,
                                                      @Sort INT,
                                                      @Value1 INT,
                                                      @Value2 INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId
      AND Page = @Page
      AND KeyIndex = @KeyIndex;

    IF @Value2 <> 0
        BEGIN
            INSERT INTO game.CharacterHotkeys (CharacterId, Page, KeyIndex, Sort, Value1, Value2)
            VALUES (@CharacterId, @Page, @KeyIndex, @Sort, @Value1, @Value2);
        END;
END;
