-- Batched liveness/kick poll input (usp_AccountSession_RefreshAndGetKicked): one row per AccountId the
-- calling process currently holds a local session for. Plain disk-based table type -- the procedure that
-- consumes it is interpreted T-SQL (see that file's header comment for why), not natively compiled, so
-- there is no requirement for this type itself to be MEMORY_OPTIMIZED.
CREATE TYPE runtime.tvp_AccountIdList AS TABLE
    (
    AccountId INT NOT NULL
    );
