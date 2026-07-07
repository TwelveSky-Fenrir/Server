-- Catalog of THROW 50xxx errors a Fenrir procedure can raise. Ranges: 501xx auth, 502xx game,
-- 504xx runtime, 509xx cross-cutting/generic.
CREATE TABLE admin.ErrorCatalog
(
    ErrorNumber INT           NOT NULL,
    SchemaName  NVARCHAR(64)  NOT NULL,
    Description NVARCHAR(400) NOT NULL,
    CONSTRAINT PK_ErrorCatalog PRIMARY KEY CLUSTERED (ErrorNumber)
);
