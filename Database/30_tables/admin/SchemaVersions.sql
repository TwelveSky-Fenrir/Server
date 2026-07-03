-- The migrator's own journal (architecture reference §12.7). Must exist before any script can be
-- marked applied -- Fenrir.Tools.DbMigrator tolerates this table itself being absent on a fresh
-- database (treats the journal as empty), so no special bootstrap step is needed: this script is
-- simply early in _manifest.txt.
CREATE TABLE admin.SchemaVersions
(
    ScriptName   NVARCHAR(260) NOT NULL,
    Sha256       BINARY(32)    NOT NULL,
    AppliedAtUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (ScriptName)
);
