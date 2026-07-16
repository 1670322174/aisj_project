-- Idempotent administrator-management schema upgrade. Existing accounts stay
-- enabled and receive authentication version 1.
SET @schema_name = DATABASE();

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='IsEnabled'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN IsEnabled tinyint(1) NOT NULL DEFAULT 1 AFTER RegisterTime'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='LastLoginAt'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN LastLoginAt datetime NULL AFTER IsEnabled'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='LastActivityAt'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN LastActivityAt datetime NULL AFTER LastLoginAt'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='UpdatedAt'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN UpdatedAt datetime NULL AFTER LastActivityAt'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='DisabledAt'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN DisabledAt datetime NULL AFTER UpdatedAt'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='DisabledByUserID'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN DisabledByUserID int NULL AFTER DisabledAt'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='LastLoginIp'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN LastLoginIp varchar(64) NULL AFTER DisabledByUserID'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND COLUMN_NAME='AuthVersion'),
    'SELECT 1',
    'ALTER TABLE users ADD COLUMN AuthVersion int NOT NULL DEFAULT 1 AFTER LastLoginIp'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS adminauditlogs (
    AuditLogID bigint NOT NULL AUTO_INCREMENT,
    AdministratorUserID int NULL,
    Action varchar(80) NOT NULL,
    TargetType varchar(50) NOT NULL,
    TargetID varchar(100) NULL,
    Summary varchar(300) NULL,
    MetadataJson json NULL,
    IpAddress varchar(64) NULL,
    UserAgent varchar(300) NULL,
    RequestID varchar(100) NULL,
    Succeeded tinyint(1) NOT NULL DEFAULT 1,
    FailureReason varchar(500) NULL,
    CreatedAt datetime NOT NULL,
    PRIMARY KEY (AuditLogID),
    KEY IX_AdminAuditLogs_CreatedAt (CreatedAt),
    KEY IX_AdminAuditLogs_Administrator_CreatedAt (AdministratorUserID, CreatedAt),
    CONSTRAINT FK_AdminAuditLogs_Administrator
        FOREIGN KEY (AdministratorUserID) REFERENCES users (UserID) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET @sql = IF(
    EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=@schema_name AND TABLE_NAME='users' AND INDEX_NAME='IX_Users_Enabled_Role_RegisterTime'),
    'SELECT 1',
    'CREATE INDEX IX_Users_Enabled_Role_RegisterTime ON users (IsEnabled, Role, RegisterTime)'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
