IF NOT EXISTS (SELECT 1
               FROM admin.TribeFourQuota
               WHERE Id = 1)
    INSERT INTO admin.TribeFourQuota (Id, MaxCount, CurrentCount)
    VALUES (1, 0, 0);
