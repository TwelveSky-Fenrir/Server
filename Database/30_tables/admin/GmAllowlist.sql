-- Legacy `gmip` (rowID, uGMIP varchar(15)) -- the GM-console connection allowlist. Small and
-- rarely-changing (the dump's only row was a dev-loopback 127.0.0.1 entry, deliberately not ported as
-- seed data -- not meaningful production data), so it is loaded whole at boot (usp_GmAllowlist_GetAll)
-- rather than checked per-row like admin.BlockedIps -- GM connections are a vanishingly small fraction
-- of total connection attempts compared to the player login path, so the native-compilation case that
-- justifies admin.BlockedIps doesn't apply here.
CREATE TABLE admin.GmAllowlist
(
    GmAllowlistId INT IDENTITY(1,1) NOT NULL,
    IpAddress     VARCHAR(45) NOT NULL,
    CreatedAtUtc  DATETIME2(3) NOT NULL CONSTRAINT DF_GmAllowlist_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GmAllowlist PRIMARY KEY CLUSTERED (GmAllowlistId),
    CONSTRAINT UQ_GmAllowlist_IpAddress UNIQUE (IpAddress)
);
