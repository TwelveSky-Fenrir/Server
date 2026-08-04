CREATE TABLE game.WorldEventSnapshots
(
    EventKind            VARCHAR(48)   NOT NULL,
    OccurrenceKey        VARCHAR(96)   NOT NULL,
    Revision             BIGINT        NOT NULL,
    Phase                VARCHAR(48)   NOT NULL,
    CanonicalPayload     NVARCHAR(MAX) NOT NULL,
    CanonicalPayloadHash BINARY(32)    NOT NULL,
    UpdatedAtUtc         DATETIME2(3)  NOT NULL
        CONSTRAINT DF_WorldEventSnapshots_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WorldEventSnapshots PRIMARY KEY CLUSTERED (EventKind, OccurrenceKey),
    CONSTRAINT CK_WorldEventSnapshots_EventKind CHECK (EventKind <> ''),
    CONSTRAINT CK_WorldEventSnapshots_OccurrenceKey CHECK (OccurrenceKey <> ''),
    CONSTRAINT CK_WorldEventSnapshots_Revision CHECK (Revision >= 0),
    CONSTRAINT CK_WorldEventSnapshots_Phase CHECK (Phase <> '')
);
