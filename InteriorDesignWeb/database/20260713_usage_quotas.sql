-- Enforceable AI generation credits and fixed five-hour assistant token windows.
-- The selected connection determines the target database.
SET @schema_name = DATABASE();

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='userquotas' AND COLUMN_NAME='AssistantTokenLimit5Hours'),
    'SELECT 1',
    'ALTER TABLE userquotas ADD COLUMN AssistantTokenLimit5Hours int NOT NULL DEFAULT 0 AFTER LastResetAt'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- One generation job must be charged at most once, including idempotent retries.
DELETE duplicate_record
FROM usagerecords duplicate_record
JOIN usagerecords keeper
  ON keeper.JobId = duplicate_record.JobId
 AND keeper.UsageType = duplicate_record.UsageType
 AND keeper.UsageID < duplicate_record.UsageID
WHERE duplicate_record.JobId IS NOT NULL;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='usagerecords' AND INDEX_NAME='UX_UsageRecords_Job_UsageType'),
    'SELECT 1',
    'CREATE UNIQUE INDEX UX_UsageRecords_Job_UsageType ON usagerecords (JobId, UsageType)'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='userquotas' AND COLUMN_NAME='AssistantTokensUsed5Hours'),
    'SELECT 1',
    'ALTER TABLE userquotas ADD COLUMN AssistantTokensUsed5Hours int NOT NULL DEFAULT 0 AFTER AssistantTokenLimit5Hours'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='userquotas' AND COLUMN_NAME='AssistantTokenWindowStartedAt'),
    'SELECT 1',
    'ALTER TABLE userquotas ADD COLUMN AssistantTokenWindowStartedAt datetime NULL AFTER AssistantTokensUsed5Hours'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO userquotas (
    UserID, TotalUnits, UsedUnits, RemainingUnits,
    AssistantTokenLimit5Hours, AssistantTokensUsed5Hours,
    AssistantTokenWindowStartedAt, CreatedAt, UpdatedAt
)
SELECT
    u.UserID,
    CASE u.Role WHEN 'FreeUser' THEN 100 WHEN 'Member' THEN 1000 WHEN 'PremiumMember' THEN 10000 ELSE 1000000 END,
    0,
    CASE u.Role WHEN 'FreeUser' THEN 100 WHEN 'Member' THEN 1000 WHEN 'PremiumMember' THEN 10000 ELSE 1000000 END,
    CASE u.Role WHEN 'FreeUser' THEN 20000 WHEN 'Member' THEN 50000 WHEN 'PremiumMember' THEN 150000 ELSE 1000000 END,
    0,
    UTC_TIMESTAMP(),
    UTC_TIMESTAMP(),
    UTC_TIMESTAMP()
FROM users u
ON DUPLICATE KEY UPDATE
    RemainingUnits = IF(TotalUnits = 0 AND UsedUnits = 0 AND RemainingUnits = 0, VALUES(RemainingUnits), RemainingUnits),
    TotalUnits = IF(TotalUnits = 0 AND UsedUnits = 0, VALUES(TotalUnits), TotalUnits),
    AssistantTokenLimit5Hours = IF(AssistantTokenLimit5Hours = 0, VALUES(AssistantTokenLimit5Hours), AssistantTokenLimit5Hours),
    AssistantTokenWindowStartedAt = COALESCE(AssistantTokenWindowStartedAt, UTC_TIMESTAMP()),
    UpdatedAt = UTC_TIMESTAMP();

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='userquotas' AND INDEX_NAME='IX_UserQuotas_AssistantWindow'),
    'SELECT 1',
    'CREATE INDEX IX_UserQuotas_AssistantWindow ON userquotas (AssistantTokenWindowStartedAt)'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
