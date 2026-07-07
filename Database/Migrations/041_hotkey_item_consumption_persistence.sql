-- auto-hunt-item-consumption (Critical): op22 CZ_USE_HOTKEY_ITEM_SEND's hotkey-bound potion/consumable
-- activation (Application/Fenrir.Application.Game.Domain/Consumables/HotkeyItemConsumptionResolver.cs --
-- life/mana gain for potion-type codes 1-5, genuine no-op for 9) had no durable persistence path at all for
-- its own post-consumption hotkey-slot decrement/clear: game.CharacterHotkeys previously had exactly one
-- writer, the one-time starter-kit grant inside usp_Character_CreateWithStarterKit -- no single-slot
-- upsert/clear procedure existed for a live session to call. Without this, the item's own quantity could
-- never actually be consumed off the character's hotkey bar, only mirrored in the live zone's in-memory
-- PlayerRuntimeState.Hotkeys until the next world entry/zone transfer silently reverted it back to the
-- last-persisted (pre-consumption) row.
--
-- Brand-new stored procedure (first appearance, never previously shipped) -- CREATE PROCEDURE, not CREATE OR
-- ALTER; no already-applied script is edited by this migration. Mirrors usp_CharacterSkills_UpsertSlot's own
-- DELETE-then-conditionally-INSERT shape (Database/StoredProcedures/game/usp_CharacterSkills_UpsertSlot.sql).
--
-- game.CharacterHotkeys stores the legacy raw triple verbatim as (Sort, Value1, Value2) -- per that table's
-- own DDL comment, the FIRST raw int is the bound id (skill id or item id), the SECOND is the secondary
-- value (grade or quantity), and the THIRD is the actual HOTKEY_SORT/kind discriminator, NOT the first int
-- despite the "Sort" column name (cross-checked against world.StarterKitHotkeys' own seed rows and
-- EnterWorldService's read-path mapping in Zone.PlayerLifecycle.cs, both already committed). @Value2 (the
-- kind discriminator) of 0 means the slot is now empty (HotkeyBindingKind.None) -- the row is deleted and
-- never re-inserted, matching "row absence = unassigned key" (PlayerRuntimeState.SetHotkeySlot's own
-- convention, and the DDL comment's own statement of that same invariant).
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
GO
