-- database/50_procedures/game/usp_Character_GetByAccount.sql
-- Contract: character-select list for a logged-in account (LC_USER_AVATAR_RECV2, M1 wire
-- contract §4.4) -- CaeriusNet: QueryAsReadOnlyCollectionAsync<T> (§11.2).
-- Params:
--   @AccountId INT -- owning account (auth.Accounts.AccountId)
-- Result set (RS0, ordinal order = [GenerateDto] contract):
--   1. CharacterId INT
--   2. Slot        TINYINT
--   3. Name        NVARCHAR(13)
--   4. Tribe       TINYINT
--   5. Gender      TINYINT
--   6. HeadType    TINYINT
--   7. FaceType    TINYINT
--   8. Level       SMALLINT
-- Rows: 0..3, bounded by CK_Characters_Slot/UQ_Characters_Account_Slot (MAX_USER_AVATAR_NUM=3)
-- -- no TOP required, the schema itself enforces the cap.
-- Ordered by Slot ascending (matches the legacy tAvatarPost slot layout).
-- Idempotent: yes (read-only).
-- Errors: none.
CREATE PROCEDURE game.usp_Character_GetByAccount @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT CharacterId,
       Slot,
       Name,
       Tribe,
       Gender,
       HeadType,
       FaceType,
       Level
FROM game.Characters
WHERE AccountId = @AccountId
ORDER BY Slot;
END;
