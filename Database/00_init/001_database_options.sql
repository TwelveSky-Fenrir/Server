-- Base options for the current database (architecture reference §12.1). None of these are the
-- SQL Server 2025 defaults on a "box" instance: every one is an explicit, reviewed choice.

ALTER
DATABASE CURRENT SET ACCELERATED_DATABASE_RECOVERY = ON;
ALTER
DATABASE CURRENT SET OPTIMIZED_LOCKING = ON;
ALTER
DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON;
ALTER
DATABASE CURRENT SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE);

-- Memory-optimized filegroup for the `runtime` schema (SCHEMA_ONLY tables, §12.4). Guarded: ADD
-- FILEGROUP/ADD FILE fail hard if re-run, and this script may be re-parsed (never re-applied once
-- journaled, but keep it safe to hand-run too).
IF
NOT EXISTS (SELECT 1 FROM sys.filegroups WHERE name = N'fenrir_mod')
BEGIN
    ALTER
DATABASE CURRENT ADD FILEGROUP fenrir_mod CONTAINS MEMORY_OPTIMIZED_DATA;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.master_files WHERE name = N'fenrir_mod' AND database_id = DB_ID())
BEGIN
    ALTER
DATABASE CURRENT ADD FILE (NAME = N'fenrir_mod', FILENAME = N'/var/opt/mssql/data/fenrir_mod')
        TO FILEGROUP fenrir_mod;
END;
GO
