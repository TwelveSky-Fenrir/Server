CREATE PROCEDURE runtime.usp_AccountSession_RecordDeviceSignature @AccountId         INT,
                                                                  @AdapterIdentifier VARCHAR(128),
                                                                  @LocalIp           VARCHAR(45),
                                                                  @RemoteIp          VARCHAR(45)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE runtime.AccountSessions WITH (SNAPSHOT)
    SET AdapterIdentifier = @AdapterIdentifier,
        LocalIp           = @LocalIp,
        RemoteIp          = @RemoteIp
    WHERE AccountId = @AccountId;
END;
