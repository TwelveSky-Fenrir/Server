IF NOT EXISTS (SELECT 1
               FROM admin.ServerQuota
               WHERE Id = 1)
    INSERT INTO admin.ServerQuota (Id, MaxPlayers)
    VALUES (1, 1000);
