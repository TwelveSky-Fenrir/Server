CREATE TABLE admin.ServerQuota
(
    Id            TINYINT NOT NULL
        CONSTRAINT DF_ServerQuota_Id DEFAULT 1,
    MaxPlayers    INT     NOT NULL,
    GagePlayerNum INT     NOT NULL
        CONSTRAINT DF_ServerQuota_GagePlayerNum DEFAULT 0,
    CONSTRAINT PK_ServerQuota PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_ServerQuota_Id CHECK (Id = 1),
    CONSTRAINT CK_ServerQuota_MaxPlayers CHECK (MaxPlayers >= 0)
);
