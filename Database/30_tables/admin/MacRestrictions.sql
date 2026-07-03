-- Legacy `macinfo` (id, mac_address varchar(23), mac_guid varchar(128), mac_limit) -- caps how many
-- accounts may be logged in simultaneously from one physical machine (mac_address) or one specific
-- installation (mac_guid). MachineGuid is NULL-able because legacy rows could target a MAC address
-- alone (mac_guid unset); the composite UNIQUE constraint relies on SQL Server's own NULL semantics for
-- unique indexes (a single (MacAddress, NULL) row is still unique, a second one for the same MAC without
-- a GUID would collide), which matches the intent: one MAC-wide default limit, plus optionally one row
-- per specific MachineGuid override for that same MAC.
-- Checked at account creation / MAC registration, not on every packet, so a plain table + plain
-- boot-time GetAll (LoginServer caches the whole table and re-evaluates per login/registration attempt)
-- is enough -- no native compilation warranted here.
CREATE TABLE admin.MacRestrictions
(
    MacRestrictionId INT IDENTITY(1,1) NOT NULL,
    MacAddress       VARCHAR(23) NOT NULL,
    MachineGuid      VARCHAR(128) NULL,
    AccountLimit     INT         NOT NULL CONSTRAINT DF_MacRestrictions_AccountLimit DEFAULT 1,
    CONSTRAINT PK_MacRestrictions PRIMARY KEY CLUSTERED (MacRestrictionId),
    CONSTRAINT UQ_MacRestrictions_MacAddress_MachineGuid UNIQUE (MacAddress, MachineGuid)
);
