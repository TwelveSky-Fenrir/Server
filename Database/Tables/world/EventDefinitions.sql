CREATE TABLE world.EventDefinitions
(
    EventDefinitionId INT IDENTITY (1,1) NOT NULL,
    EventType         INT                NOT NULL
        CONSTRAINT DF_EventDefinitions_EventType DEFAULT 0,
    SortKey           NVARCHAR(10)       NULL,
    Rate              INT                NOT NULL
        CONSTRAINT DF_EventDefinitions_Rate DEFAULT 0,
    ZoneNumber        SMALLINT           NULL,
    Message           NVARCHAR(60)       NULL,
    CONSTRAINT PK_EventDefinitions PRIMARY KEY CLUSTERED (EventDefinitionId)
);
