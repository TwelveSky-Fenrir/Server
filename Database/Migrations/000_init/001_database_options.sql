
ALTER
    DATABASE CURRENT SET ACCELERATED_DATABASE_RECOVERY = ON;
ALTER
    DATABASE CURRENT SET OPTIMIZED_LOCKING = ON;
ALTER
    DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON;
ALTER
    DATABASE CURRENT SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE);

IF
    NOT EXISTS (SELECT 1
                FROM sys.filegroups
                WHERE name = N'fenrir_mod')
    BEGIN
        ALTER
            DATABASE CURRENT ADD FILEGROUP fenrir_mod CONTAINS MEMORY_OPTIMIZED_DATA;
    END;
GO

IF NOT EXISTS (SELECT 1
               FROM sys.master_files
               WHERE name = N'fenrir_mod'
                 AND database_id = DB_ID())
    BEGIN
        ALTER
            DATABASE CURRENT ADD FILE (NAME = N'fenrir_mod', FILENAME = N'/var/opt/mssql/data/fenrir_mod')
            TO FILEGROUP fenrir_mod;
    END;
GO
