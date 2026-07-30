-- Singleton row (Id=1), seeded by 70_seed/admin/008_server_quota.sql -- the durable, externally-editable
-- store behind the login-time maintenance-lockdown (MaxPlayers = 0) and server-full quota gates
-- (Fenrir.Application.Login.Services.Login.LoginService.LoginAsync). An operator edits this row directly;
-- ServerQuotaRefreshHost re-reads it roughly once a second, so neither a restart nor a redeploy is needed to
-- flip maintenance mode or retune the cap.
--
-- Mirrors legacy defineserver.mMaxPlayerNum (Server/ts25login/S08_MyDB.cpp:42-61, GetMaxUser;
-- Server/BuildEU33/DB/nxtserver.sql:308-320). GagePlayerNum (below) is legacy's sibling mGagePlayerNum --
-- carried over as a wire-visible field only, not consumed by either the maintenance-lockdown or server-full
-- gate in that citation. mAddPlayerNum is the one sibling column still intentionally not carried over.
CREATE TABLE admin.ServerQuota
(
    Id            TINYINT NOT NULL
        CONSTRAINT DF_ServerQuota_Id DEFAULT 1,
    MaxPlayers    INT     NOT NULL,
    -- Wire-visible sibling column, distinct from mAddPlayerNum above: unlike MaxPlayers below, no CHECK
    -- constraint here -- legacy never validates this field either, and unlike MaxPlayers it has no
    -- consuming quota gate a negative value could silently break. See usp_ServerQuota_GetMaxPlayers.sql
    -- for the read side.
    GagePlayerNum INT     NOT NULL
        CONSTRAINT DF_ServerQuota_GagePlayerNum DEFAULT 0,
    CONSTRAINT PK_ServerQuota PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_ServerQuota_Id CHECK (Id = 1),
    CONSTRAINT CK_ServerQuota_MaxPlayers CHECK (MaxPlayers >= 0)
);
