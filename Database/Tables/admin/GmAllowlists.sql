-- Legacy `gmip`: GM-console connection allowlist. The dump's only row (127.0.0.1 dev-loopback) was
-- deliberately not ported as seed data.
CREATE TABLE admin.GmAllowlists
(
    GmAllowlistId INT IDENTITY (1,1) NOT NULL,
    IpAddress     VARCHAR(45)        NOT NULL,
    CreatedAtUtc  DATETIME2(3)       NOT NULL
        CONSTRAINT DF_GmAllowlists_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GmAllowlists PRIMARY KEY CLUSTERED (GmAllowlistId),
    CONSTRAINT UQ_GmAllowlists_IpAddress UNIQUE (IpAddress)
);
