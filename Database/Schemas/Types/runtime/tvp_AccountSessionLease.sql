CREATE TYPE runtime.tvp_AccountSessionLease AS TABLE
(
    AccountId    INT              NOT NULL,
    SessionToken UNIQUEIDENTIFIER NOT NULL
);
