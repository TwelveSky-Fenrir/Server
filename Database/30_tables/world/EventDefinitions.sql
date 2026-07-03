-- Legacy MySQL `eventinfo` (nxtserver.sql): an admin-editable GM-event config table that already
-- existed in production but had 0 rows in the captured dump -- table + retrieval proc only here, no
-- seed data (future GM tooling is what populates this, not this migration).
-- EventDefinitionId is IDENTITY here, unlike the other legacy-MySQL-sourced world.* tables in this
-- same batch: the legacy `id` column is AUTO_INCREMENT with zero existing rows to preserve a specific
-- numbering for, so there is no "wire protocol/gameplay logic already references this exact number"
-- contract to honor -- a surrogate key is the correct call (contrast world.ItemMallProducts/
-- world.BloodExchangeCatalog/world.RewardBundles, which all keep their legacy id as-given).
-- ZoneNumber is left a plain nullable column, not a FOREIGN KEY to world.Zones: with zero real rows
-- to observe, it's unverifiable whether the legacy default 0 means "no zone" or a real sentinel (e.g.
-- "all zones"), so a hard FK is deliberately not asserted on unattested data.
CREATE TABLE world.EventDefinitions
(
    EventDefinitionId INT IDENTITY (1,1) NOT NULL,
    EventType         INT NOT NULL CONSTRAINT DF_EventDefinitions_EventType DEFAULT 0, -- legacy `type`
    SortKey           NVARCHAR(10) NULL,                                               -- legacy `sort`, a free-form GM-tooling ordering tag, not a numeric sequence
    Rate              INT NOT NULL CONSTRAINT DF_EventDefinitions_Rate DEFAULT 0,
    ZoneNumber        SMALLINT NULL,                                                   -- legacy `zone`; see file header for why this is not a FK
    Message           NVARCHAR(60) NULL,
    CONSTRAINT PK_EventDefinitions PRIMARY KEY CLUSTERED (EventDefinitionId)
);
