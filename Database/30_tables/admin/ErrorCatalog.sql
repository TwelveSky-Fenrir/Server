-- Catalog of every THROW 50xxx a Fenrir procedure can raise (architecture reference §12.3):
-- documentation as data, so a mismatch between a proc's THROW and this table is a glance away.
-- Ranges: 501xx auth, 502xx game, 504xx runtime, 509xx cross-cutting/generic.
CREATE TABLE admin.ErrorCatalog
(
    ErrorNumber INT NOT NULL,
    SchemaName  NVARCHAR(64)  NOT NULL,
    Description NVARCHAR(400) NOT NULL,
    CONSTRAINT PK_ErrorCatalog PRIMARY KEY CLUSTERED (ErrorNumber)
);
