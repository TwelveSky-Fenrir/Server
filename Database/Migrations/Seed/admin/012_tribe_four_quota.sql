-- Seeds the admin.TribeFourQuota singleton row -- MaxCount=0 (no conversions grantable) until an operator
-- raises it, matching this table's own header comment.
IF NOT EXISTS (SELECT 1 FROM admin.TribeFourQuota WHERE Id = 1)
    INSERT INTO admin.TribeFourQuota (Id, MaxCount, CurrentCount)
    VALUES (1, 0, 0);
