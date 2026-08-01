-- Legacy SOCKET_INFO; GemSocketId is the 1-based array slot (the struct has no explicit index field).
-- UNIQUE(Type, Value02) mirrors the legacy lookup key: GSOCKET::Search(mType, mValue02) in ts25zone GameSystem_08_Socket.cpp.
--
-- CK_GemSockets_TypeBandRules mirrors legacy's load-time field validation (GSocket_CheckValidElement,
-- Server/Header/S15_MyShare.cpp:2212-2268), which rejects the whole 2891-slot Load_GSocket call on the
-- first offending record -- fatal to ts25zone's fixed startup data-load sequence. Rules, evaluated in this
-- exact order per the legacy source:
--   * Value02 = 0 unconditionally accepts the record, ahead of every other rule (:2214-2215).
--   * Type = 0 (with Value02 <> 0) likewise unconditionally accepts (:2216-2217).
--   * Type outside 1-46 (neither bypass above applied) rejects (:2218).
--   * Value01's stated range check (:2220) is confirmed dead code -- it requires Value01 to be
--     simultaneously negative and > 10000, which no integer satisfies -- so it never rejects any record and
--     is deliberately NOT reproduced here; there is no effective legacy rule to mirror.
--   * Type = 1: Value02 in 1-33, Value03 in 0-400, Value04 in 0-400 (:2222-2230).
--   * Type 2-29: Value02 in 1-100, Value03 in 0-1000, Value04 in 0-1000 (:2231-2239).
--   * Type 30-38: the band written to guard this window (:2240-2248) is also confirmed dead code -- it
--     requires Type to be simultaneously >= 30 and <= 28 -- so every record with Type in this window falls
--     through, unconstrained on Value02/Value03/Value04, to the final unconditional accept (:2267). This is
--     an unintentionally permissive gap, not a deliberate "no constraints" rule, and the true intended
--     bounds cannot be recovered from the source as written -- deliberately left unconstrained here to
--     match legacy's actual (buggy) runtime behavior rather than guess at bounds that cannot be cited.
--   * Type 39-42: Value02 in 1-10, Value03 >= 1 (no stated upper bound), Value04 = 0 (:2249-2257).
--   * Type 43-46: Value02 in 1-10, Value03 >= 6 (no stated upper bound), Value04 = 0 (:2258-2266).
-- All five Type-band rules interact with the same four columns per record, so this is one composite CHECK
-- rather than fragmented per-column constraints that could not express "which band applies" on their own.
CREATE TABLE world.GemSockets
(
    GemSocketId INT NOT NULL,
    Type        INT NOT NULL,
    Value01     INT NOT NULL,
    Value02     INT NOT NULL,
    Value03     INT NOT NULL,
    Value04     INT NOT NULL,
    CONSTRAINT PK_GemSockets PRIMARY KEY CLUSTERED (GemSocketId),
    CONSTRAINT UQ_GemSockets_Type_Value02 UNIQUE (Type, Value02),
    CONSTRAINT CK_GemSockets_TypeBandRules CHECK (
        Value02 = 0
            OR Type = 0
            OR (
            Type BETWEEN 1 AND 46
                AND (
                (Type = 1 AND Value02 BETWEEN 1 AND 33 AND Value03 BETWEEN 0 AND 400 AND Value04 BETWEEN 0 AND 400)
                    OR (Type BETWEEN 2 AND 29 AND Value02 BETWEEN 1 AND 100 AND Value03 BETWEEN 0 AND 1000 AND
                        Value04 BETWEEN 0 AND 1000)
                    OR
                (Type BETWEEN 30 AND 38) -- dead band in legacy source; deliberately left unconstrained, see header
                    OR (Type BETWEEN 39 AND 42 AND Value02 BETWEEN 1 AND 10 AND Value03 >= 1 AND Value04 = 0)
                    OR (Type BETWEEN 43 AND 46 AND Value02 BETWEEN 1 AND 10 AND Value03 >= 6 AND Value04 = 0)
                )
            )
        )
);
