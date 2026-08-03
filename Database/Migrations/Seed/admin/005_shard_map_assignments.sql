IF NOT EXISTS (SELECT 1
               FROM admin.ShardMapAssignments)
    BEGIN
        INSERT INTO admin.ShardMapAssignments (ShardId, MapId)
        VALUES (1, 1),
               (1, 6),
               (1, 11),
               (1, 140);
    END;
