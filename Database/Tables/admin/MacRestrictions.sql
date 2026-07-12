-- Legacy `macinfo`: caps accounts per MAC address and/or per install (MachineGuid, NULL-able). The
-- composite UNIQUE relies on SQL Server treating each NULL as distinct, allowing one MAC-wide default
-- row plus per-MachineGuid override rows for the same MAC.
CREATE TABLE admin.MacRestrictions
(
    MacRestrictionId INT IDENTITY (1,1) NOT NULL,
    MacAddress       VARCHAR(23)        NOT NULL,
    MachineGuid      VARCHAR(128)       NULL,
    AccountLimit     INT                NOT NULL
        CONSTRAINT DF_MacRestrictions_AccountLimit DEFAULT 1
        CONSTRAINT CK_MacRestrictions_AccountLimit CHECK (AccountLimit >= 0),
    CONSTRAINT PK_MacRestrictions PRIMARY KEY CLUSTERED (MacRestrictionId),
    CONSTRAINT UQ_MacRestrictions_MacAddress_MachineGuid UNIQUE (MacAddress, MachineGuid)
);
