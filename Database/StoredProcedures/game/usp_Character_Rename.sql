-- op19 CL_CHANGE_AVATAR_NAME_SEND's rename-scroll consumption gate: atomically re-verifies the item at the
-- client-claimed (Container, Slot) is still the rename scroll (1133), re-checks name uniqueness, and -- only
-- if both hold -- deletes that item slot and renames the character, all in one transaction.
--
-- Unlike the legacy ChangeCharacterName (Server/ts25login/S08_MyDB.cpp:848-899), which mutates the in-memory
-- name and item-id first and only manually (and incompletely) compensates if the later UPDATE fails, this
-- proc wraps the item-slot delete and the name UPDATE in one transaction: on every failure branch below,
-- nothing at all is mutated.
--
-- Return codes (SELECTed, not THROWn -- RenameAvatarService forwards these verbatim, mapping unexpected
-- engine exceptions to its own SqlError outcome):
--   0    renamed.
--   102  no character at that (AccountId, Slot) -- same code the pre-item-gate version of this proc used
--        for "affected zero rows"; kept for the character-vanished-mid-transaction race too.
--   -1   the item at the claimed slot no longer matches RequiredItemId. Fenrir-internal sentinel, never a
--        legacy wire code -- RenameAvatarService already checked this moments earlier via
--        usp_CharacterItem_GetIdAtSlot, so reaching this branch is a rare TOCTOU race, not the common path.
--        The caller must treat it exactly like the item-gate's own failure: session disconnect, no response.
--   2    the new name is already taken by another character, or unchanged from this character's own current
--        name -- the uniqueness check does not exclude self.
CREATE PROCEDURE game.usp_Character_Rename @AccountId INT,
                                           @Slot TINYINT,
                                           @NewName NVARCHAR(13),
                                           @ItemContainer TINYINT,
                                           @ItemSlot TINYINT,
                                           @RequiredItemId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CharacterId INT;

    SELECT @CharacterId = CharacterId
    FROM game.Characters
    WHERE AccountId = @AccountId
      AND Slot = @Slot;

    IF @CharacterId IS NULL
        BEGIN
            SELECT 102;
            RETURN;
        END;

    IF NOT EXISTS (SELECT 1
                   FROM game.CharacterItems
                   WHERE CharacterId = @CharacterId
                     AND Container = @ItemContainer
                     AND Slot = @ItemSlot
                     AND ItemId = @RequiredItemId)
        BEGIN
            SELECT -1;
            RETURN;
        END;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @NewName)
        BEGIN
            SELECT 2;
            RETURN;
        END;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET Name         = @NewName,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 102;
            RETURN;
        END;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ItemContainer
      AND Slot = @ItemSlot;

    COMMIT TRANSACTION;

    SELECT 0;
END;
