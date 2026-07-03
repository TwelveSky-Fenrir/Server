-- Source: legacy SOCKET_INFO (Header/Protocol/STRUCT.h:1970-1977), 005_00010.IMG via SocketReader.ReadAll (no
-- runtime patch exists for this file, ReadAll is byte-identical to ReadAllRaw). SOCKET_INFO has no explicit
-- index field of its own -- confirmed from the struct (Type, Value01..04 only) -- so GemSocketId is the
-- 1-based array slot position, matching the cross-domain PK contract.
-- (Type, Value02) is the real natural lookup key: GSOCKET::Search(int tType, int tValue) in
-- ts25zone/GameSystem/GameSystem_08_Socket.cpp matches on exactly (mType, mValue02), and real data confirms
-- Value02 is a dense 1..N sequential sub-index within each Type with zero duplicate (Type,Value02) pairs
-- across all 2891 rows -- hence the UNIQUE constraint below, which is the actual legacy lookup shape, not
-- just an incidental property of the data.
-- Value01/Value03/Value04 are kept under their legacy generic names rather than renamed: real data narrows
-- their role (Value01 is always 0 for Type=1 and a 1..250 scalar otherwise; Value03/Value04 are two possible
-- stat/bonus amounts a caller selects between, per S07_MyGame03.cpp's ReturnSetItemIUValue*-style callers) but
-- does not conclusively pin down an exact semantic label worth guessing at in a column name.
CREATE TABLE world.GemSockets
(
    GemSocketId INT NOT NULL,
    Type        INT NOT NULL,
    Value01     INT NOT NULL,
    Value02     INT NOT NULL,
    Value03     INT NOT NULL,
    Value04     INT NOT NULL,
    CONSTRAINT PK_GemSockets PRIMARY KEY CLUSTERED (GemSocketId),
    CONSTRAINT UQ_GemSockets_Type_Value02 UNIQUE (Type, Value02)
);
