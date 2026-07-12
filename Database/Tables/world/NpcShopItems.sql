-- Normalized from nShopInfo[3][28]; sparse (unlike NpcMenuOptions) -- only 34 of 131 real NPCs sell anything, and few of their 84 possible slots are populated.
-- No secondary index on ItemId: verified repo-wide that every reader (usp_NpcShopItem_GetAll's unfiltered
-- scan; vw_NpcWithShopSummary's GROUP BY NpcId; the deleted-as-dead-code-2026-07-12 usp_Npc_GetById used to
-- filter WHERE NpcId=@NpcId here too, already covered by the clustered PK's leading column) reads this table
-- by NpcId, never by ItemId. A reverse "which NPCs
-- sell item X" index would only tax the one-time seed insert (Migrations/Seed/world/Npcs.sql) for zero read
-- benefit today; re-add if that GM-tooling query is ever built.
CREATE TABLE world.NpcShopItems
(
    NpcId     INT     NOT NULL,
    ShopPage  TINYINT NOT NULL,
    SlotIndex TINYINT NOT NULL,
    ItemId    INT     NULL,
    CONSTRAINT PK_NpcShopItems PRIMARY KEY CLUSTERED (NpcId, ShopPage, SlotIndex),
    CONSTRAINT CK_NpcShopItems_ShopPage CHECK (ShopPage BETWEEN 0 AND 2),
    CONSTRAINT CK_NpcShopItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 27),
    CONSTRAINT FK_NpcShopItems_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_NpcShopItems_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
