-- Migrations/047_characters_petbagdate_column.sql
-- Adds the persisted pet-bag upper-half (slots 10-19) rental-entitlement expiry date (legacy aPetBagDate,
-- Server/Header/Protocol/STRUCT.h:520-521), the sibling column game.Characters.InventoryDate/StoreDate
-- (Tables/game/Characters.sql) already have for the second Inventory/Store page entitlements. Closes the
-- confirmation-pass gap that left GenericActionHandler's petBagUpperHalfEntitlementActive hardcoded to the
-- literal true (see that handler's own remarks and PlayerRuntimeState.PetBag.cs's own remarks for the full
-- citation trail) -- gated the same way, via PlayerRuntimeState.PetBagDate >= GameDate.Today().
--
-- DEFAULT 0 is not a guess: ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:381-383 confirms
-- legacy's own live character-creation path never sets aPetBagDate, so it stays 0 (permanently expired) for
-- every character there too -- unlike InventoryDate/StoreDate, which usp_Character_CreateWithStarterKit's own
-- EU33/USE_CUSTOME_CREATE grant DOES set to a real welcome-bonus expiry at creation (Server/ts25login/
-- S04_MyWork02.cpp:740-1179). No citation anywhere (Server/ or ServerDocs/) establishes what item/command/
-- duration, if any, ever grants this entitlement a non-zero value in legacy -- so this migration deliberately
-- adds ONLY the always-0-by-default column/read path (mechanical, safe, matches legacy's own confirmed
-- baseline), and usp_Character_CreateWithStarterKit is deliberately NOT changed to grant it. Wiring an actual
-- grant mechanism is a separate, not-yet-specified follow-up -- do not infer one here.
--
-- Standalone additive migration -- never edit the already-applied game.Characters creation script
-- (Tables/game/Characters.sql) per the DbMigrator SHA-256 journal rule. Idempotent so a re-run no-ops.
IF COL_LENGTH('game.Characters', 'PetBagDate') IS NULL
ALTER TABLE game.Characters
    ADD PetBagDate INT NOT NULL
        CONSTRAINT DF_Characters_PetBagDate DEFAULT 0;
