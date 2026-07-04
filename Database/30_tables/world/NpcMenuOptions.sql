-- Normalized from nMenu[100]; unlike the other sparse arrays here, all 100 slots are populated for every NPC (values only ever 1 or 2) --
-- reads as per-slot enable/state flags over a fixed menu-command catalog, not a list of referenced ids. OptionId stores the raw legacy value verbatim (no FK: not documented as referencing another world.* domain).
CREATE TABLE world.NpcMenuOptions
(
    NpcId     INT      NOT NULL,
    SlotIndex SMALLINT NOT NULL,
    OptionId  INT      NOT NULL,
    CONSTRAINT PK_NpcMenuOptions PRIMARY KEY CLUSTERED (NpcId, SlotIndex),
    CONSTRAINT CK_NpcMenuOptions_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 99),
    CONSTRAINT FK_NpcMenuOptions_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId)
);
