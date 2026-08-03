CREATE TABLE runtime.ProxyShopExpirationRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId     TINYINT               NOT NULL,
    CharacterId       INT                   NOT NULL,
    NewExpirationDate INT                   NOT NULL,
    CorrelationId     UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_ProxyShopExpirationRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_ProxyShopExpirationRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_ProxyShopExpirationRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
