-- Legacy MySQL `eventinfo`; captured dump had 0 rows, so no seed data here (future GM tooling populates this).
-- EventDefinitionId is IDENTITY (unlike other legacy-sourced world.* tables) since there's no existing row numbering to preserve. ZoneNumber is not a FK: with zero real rows, whether legacy 0 means "no zone" is unverifiable.
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
