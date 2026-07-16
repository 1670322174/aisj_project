-- AI任务记录软删除字段。只隐藏任务历史，不删除生成结果和方案图片。
-- 执行前请备份数据库。

USE aisj_club;

DELIMITER $$

DROP PROCEDURE IF EXISTS add_column_if_missing $$
CREATE PROCEDURE add_column_if_missing(
    IN table_name_param VARCHAR(64),
    IN column_name_param VARCHAR(64),
    IN alter_sql_param TEXT
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = table_name_param
          AND COLUMN_NAME = column_name_param
    ) THEN
        SET @ddl = alter_sql_param;
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END $$

DELIMITER ;

CALL add_column_if_missing(
    'aigenerationjobs',
    'IsDeleted',
    'ALTER TABLE aigenerationjobs ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 COMMENT ''是否从任务历史隐藏'''
);

CALL add_column_if_missing(
    'aigenerationjobs',
    'DeletedAt',
    'ALTER TABLE aigenerationjobs ADD COLUMN DeletedAt datetime DEFAULT NULL COMMENT ''任务记录删除时间'''
);

DROP PROCEDURE IF EXISTS add_column_if_missing;

DELIMITER $$

DROP PROCEDURE IF EXISTS add_index_if_missing $$
CREATE PROCEDURE add_index_if_missing(
    IN table_name_param VARCHAR(64),
    IN index_name_param VARCHAR(64),
    IN alter_sql_param TEXT
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = table_name_param
          AND INDEX_NAME = index_name_param
    ) THEN
        SET @ddl = alter_sql_param;
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END $$

DELIMITER ;

CALL add_index_if_missing(
    'aigenerationjobs',
    'IX_AiGenerationJobs_User_Deleted_Created',
    'ALTER TABLE aigenerationjobs ADD INDEX IX_AiGenerationJobs_User_Deleted_Created (UserID, IsDeleted, CreatedAt)'
);

DROP PROCEDURE IF EXISTS add_index_if_missing;
