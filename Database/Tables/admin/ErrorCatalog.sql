CREATE TABLE admin.ErrorCatalog
(
    ErrorNumber INT           NOT NULL,
    SchemaName  VARCHAR(10)   NOT NULL,
    Description NVARCHAR(400) NOT NULL,
    CONSTRAINT PK_ErrorCatalog PRIMARY KEY CLUSTERED (ErrorNumber),
    CONSTRAINT CK_ErrorCatalog_ErrorNumber CHECK (ErrorNumber BETWEEN 50000 AND 59999),
    CONSTRAINT CK_ErrorCatalog_SchemaName CHECK (SchemaName IN ('auth', 'game', 'runtime', 'admin', 'world'))
);
