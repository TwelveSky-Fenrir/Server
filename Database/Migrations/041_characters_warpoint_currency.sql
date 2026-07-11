-- Migrations/041_characters_warpoint_currency.sql
-- Adds the persisted War-Point currency balance (USE_WAR_POINT_SYSTEM; legacy aWarPoint,
-- Server/Header/Protocol/STRUCT.h:554). War-Point is a spendable balance, so -- exactly like
-- game.Characters.Money and game.Characters.BloodCoin -- it is deliberately kept OUT of the write-behind
-- progress batch (usp_Character_PersistProgressBatch) so that a last-write-wins flush can never clobber it.
-- It moves only through the dedicated atomic War-Point purchase procedure (game.usp_Character_BuyWarPointItem,
-- Migrations/042). Standalone additive migration -- never edit the already-applied game.Characters creation
-- script (Tables/game/Characters.sql) per the DbMigrator SHA-256 journal rule. Idempotent so a re-run no-ops.
--
-- NOTE (world-entry hydration): usp_Character_GetForWorldEntry does not yet project this column, so
-- PlayerRuntimeState.WarPoint still resets to 0 on each world entry. The purchase/affordability path is
-- server-authoritative against THIS column (usp_Character_BuyWarPointItem), so spending is correct and durable
-- regardless of the in-memory mirror; unifying the in-memory mirror (hydrate at world entry + route grants
-- through a persist path) is a separate follow-up on owned world-entry files.
IF COL_LENGTH('game.Characters', 'WarPoint') IS NULL
    ALTER TABLE game.Characters
        ADD WarPoint INT NOT NULL
            CONSTRAINT DF_Characters_WarPoint DEFAULT 0
            CONSTRAINT CK_Characters_WarPoint CHECK (WarPoint >= 0);
