-- Database-layer defense-in-depth backstop for the "inventory-capacity-stacking-rules" critical finding
-- (legacy-behavior-translator contract, 2026-07 round): ContainerMatrix.ResolveMove -- the resolver behind
-- the ordinary inventory<->inventory / inventory<->equip container-move action (tSort 208/210/213), the
-- single most common inventory action in the game -- merges a stackable-sort source stack into an occupied
-- destination stack with no check against the legacy MAX_ITEM_DUPLICATION_NUM cap of 999
-- (Server/Header/Protocol/DEFINE.h:611), unlike every other merge path in the codebase (ground-pickup,
-- Store transfer, account-vault/bank transfer), which already correctly reject an over-cap merge before it
-- is ever applied. Legacy's own ProcessForInventoryToInventory (Server/ts25zone/S04_MyWork05.cpp:834-866)
-- explicitly checks '(to->iQuantity + tQuantity1) > MAX_ITEM_DUPLICATION_NUM' and disconnects the session on
-- overflow; a sum of exactly 999 is legacy-accepted (strict greater-than comparison), never rejected.
--
-- Fixing ContainerMatrix.ResolveMove/GenericActionService.MoveContainerAsync themselves is an
-- Application/Fenrir.Application.Game.* change, out of this database-engineer's Infrastructure/Database/
-- charter -- tracked separately for whichever engineer owns that resolver (see the behavior contract's own
-- closing note: it leaves the outward failure signal -- clean in-protocol rejection vs. legacy's hard
-- disconnect -- as that engineer's decision, not something a schema constraint can express). What belongs
-- here is closing the schema-level gap the finding calls out directly: game.CharacterItems.Quantity
-- (Tables/game/CharacterItems.sql:14) carried only CK_CharacterItems_Quantity CHECK (Quantity >= 1) -- a
-- lower bound only, no upper bound anywhere in the persisted schema -- so a durable over-cap merge (from
-- this bug, or any future bug reaching this table) was never backstopped at the one layer common to every
-- writer, regardless of which application code path produced it. That table script is already applied/
-- journaled (DbMigrator hashes and refuses a changed re-apply), so the constraint is replaced here via a
-- fresh DROP/ADD pair, never an in-place edit of CharacterItems.sql.
--
-- The new bound is inclusive at 999 (BETWEEN 1 AND 999), matching legacy's confirmed strict-greater-than
-- overflow comparison. This applies uniformly across every Container value CharacterItems stores (Inventory
-- pages 0/1, Equipment 2, Store pages 3/4) -- MAX_ITEM_DUPLICATION_NUM is a single global constant, not
-- per-container, and the contract leaves open (unresolved, not assumed) whether a same-item merge is even
-- reachable for the Equipment container specifically, so no container-conditional logic is introduced here.
--
-- Note this constraint is a last-resort backstop, not a replacement for the application-level fix: a
-- violation surfaces to the caller as a raw SQL constraint-violation error (native error 547), not a
-- catalogued admin.ErrorCatalog THROW -- registering a new domain error code here would presume an outward
-- failure-signal decision (clean rejection vs. disconnect) that this contract explicitly leaves to the
-- resolver's own owning engineer, not to this schema-only change.
ALTER TABLE game.CharacterItems
    DROP CONSTRAINT CK_CharacterItems_Quantity;
GO

ALTER TABLE game.CharacterItems
    ADD CONSTRAINT CK_CharacterItems_Quantity CHECK (Quantity BETWEEN 1 AND 999);
GO
