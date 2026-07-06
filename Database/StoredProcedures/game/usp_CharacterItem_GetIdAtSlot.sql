-- Targeted single-slot read: row-absence = empty slot (game.CharacterItems' own convention, see that
-- table's header comment). Used by op19 CL_CHANGE_AVATAR_NAME_SEND's rename-scroll gate, which trusts the
-- client-supplied (Page, Index) coordinates completely rather than scanning the whole container.
CREATE PROCEDURE game.usp_CharacterItem_GetIdAtSlot @CharacterId INT,
    @Container   TINYINT,
    @Slot        TINYINT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT ItemId
FROM game.CharacterItems
WHERE CharacterId = @CharacterId
  AND Container = @Container
  AND Slot = @Slot;
END;
