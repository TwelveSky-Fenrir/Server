CREATE TABLE admin.TribeFourQuota
(
    Id           TINYINT NOT NULL
        CONSTRAINT DF_TribeFourQuota_Id DEFAULT 1,
    MaxCount     INT     NOT NULL,
    CurrentCount INT     NOT NULL
        CONSTRAINT DF_TribeFourQuota_CurrentCount DEFAULT 0,
    CONSTRAINT PK_TribeFourQuota PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_TribeFourQuota_Id CHECK (Id = 1),
    CONSTRAINT CK_TribeFourQuota_CurrentCount CHECK (CurrentCount >= 0),
    CONSTRAINT CK_TribeFourQuota_CurrentWithinMax CHECK (CurrentCount <= MaxCount)
);
