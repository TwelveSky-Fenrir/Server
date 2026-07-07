-- Singleton row (Id=1), seeded by 70_seed/admin/008_server_quota.sql -- the durable, externally-editable
-- store behind the login-time maintenance-lockdown (MaxPlayers = 0) and server-full quota gates
-- (Fenrir.Application.Login.Services.Login.LoginService.LoginAsync). An operator edits this row directly;
-- ServerQuotaRefreshHost re-reads it roughly once a second, so neither a restart nor a redeploy is needed to
-- flip maintenance mode or retune the cap.
--
-- Mirrors legacy defineserver.mMaxPlayerNum (Server/ts25login/S08_MyDB.cpp:42-61, GetMaxUser;
-- Server/BuildEU33/DB/nxtserver.sql:308-320). Legacy's sibling columns (mAddPlayerNum/mGagePlayerNum) aren't
-- consumed by either gate in that citation and are intentionally not carried over here.
CREATE TABLE admin.ServerQuota
(
    Id         TINYINT NOT NULL
        CONSTRAINT DF_ServerQuota_Id DEFAULT 1,
    MaxPlayers INT     NOT NULL,
    CONSTRAINT PK_ServerQuota PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_ServerQuota_Id CHECK (Id = 1)
);
