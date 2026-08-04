CREATE TABLE runtime.PopupEventLease
(
    OccurrenceKey     VARCHAR(96)          NOT NULL,
    LeaseOwnerId      UNIQUEIDENTIFIER     NOT NULL,
    LeaseExpiresAtUtc DATETIME2(3)         NOT NULL,
    AcquiredAtUtc     DATETIME2(3)         NOT NULL
        CONSTRAINT DF_PopupEventLease_AcquiredAtUtc DEFAULT SYSUTCDATETIME(),
    RenewedAtUtc      DATETIME2(3)         NOT NULL
        CONSTRAINT DF_PopupEventLease_RenewedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PopupEventLease PRIMARY KEY CLUSTERED (OccurrenceKey),
    INDEX IX_PopupEventLease_LeaseExpiresAtUtc NONCLUSTERED (LeaseExpiresAtUtc)
);
