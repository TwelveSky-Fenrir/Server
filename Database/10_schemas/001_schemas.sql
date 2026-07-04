CREATE SCHEMA auth AUTHORIZATION dbo;
GO

CREATE SCHEMA game AUTHORIZATION dbo;
GO

CREATE SCHEMA runtime AUTHORIZATION dbo;
GO

CREATE SCHEMA admin AUTHORIZATION dbo;
GO

-- world: read-mostly reference data migrated from legacy 005_0000N.IMG/002.BIN/003.BIN/.WREGION.csv
-- plus small legacy MySQL config tables.
CREATE SCHEMA world AUTHORIZATION dbo;
GO
