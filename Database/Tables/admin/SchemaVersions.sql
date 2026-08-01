-- Migrator's journal; Fenrir.Tools.DbMigrator tolerates this table being absent on a fresh database
-- (treats the journal as empty).
CREATE TABLE admin.SchemaVersions
(
    ScriptName   NVARCHAR(260) NOT NULL,
    Sha256       BINARY(32)    NOT NULL,
    AppliedAtUtc DATETIME2(3)  NOT NULL
        CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (ScriptName)
);
