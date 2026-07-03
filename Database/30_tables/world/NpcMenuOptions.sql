-- Normalized from nMenu[100] (Header/Protocol/STRUCT.h:213-234): only nonzero slots are kept, per the
-- standard sparse-fixed-array rule -- BUT a surprising finding surfaced while generating the real seed
-- data (flagged here rather than silently seeding a huge block unremarked): 0 filtering removes NOTHING.
-- Every one of the 100 slots is populated for every one of the 131 real NPCs (13100/13100), and the only
-- two raw values ever observed anywhere in this build are 1 and 2 (97.6% are 1). That shape -- a fixed-
-- width, always-fully-populated, near-boolean array -- reads far more like a per-slot enable/state flag
-- over a fixed 100-entry menu-command catalog (e.g. "does this NPC show command #37 in its right-click
-- menu, and if so which of two variants") than a sparse list of distinct referenced ids. The exact
-- catalog (what command #0..#99 each mean) is not confirmed by prior investigation, so OptionId stores
-- the raw legacy value verbatim rather than guessing at a friendlier column/table shape; a child table is
-- still the right call over 100 columns since GameServer only ever needs "the handful of slots this NPC's
-- UI actually cares about", not the full row.
-- No FK on OptionId: unlike ShopItems/SkillOffers, nMenu is not documented anywhere as referencing another
-- world.* domain's id space.
CREATE TABLE world.NpcMenuOptions
(
    NpcId     INT      NOT NULL,
    SlotIndex SMALLINT NOT NULL,
    OptionId  INT      NOT NULL,
    CONSTRAINT PK_NpcMenuOptions PRIMARY KEY CLUSTERED (NpcId, SlotIndex),
    CONSTRAINT CK_NpcMenuOptions_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 99),
    CONSTRAINT FK_NpcMenuOptions_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId)
);
