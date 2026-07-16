-- AI 任务真删除与生成资产解耦。
-- 删除任务时保留 AI 图片记录；未被方案引用的图片进入延迟清理队列。
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
    'aigenerationjobimages',
    'RetentionStatus',
    'ALTER TABLE aigenerationjobimages ADD COLUMN RetentionStatus varchar(30) NOT NULL DEFAULT ''active'' COMMENT ''active/retained/cleanup_pending'''
);

CALL add_column_if_missing(
    'aigenerationjobimages',
    'DetachedAt',
    'ALTER TABLE aigenerationjobimages ADD COLUMN DetachedAt datetime DEFAULT NULL COMMENT ''与生成任务解除关联时间'''
);

CALL add_column_if_missing(
    'aigenerationjobimages',
    'CleanupEligibleAt',
    'ALTER TABLE aigenerationjobimages ADD COLUMN CleanupEligibleAt datetime DEFAULT NULL COMMENT ''最早允许清理时间'''
);

DROP PROCEDURE IF EXISTS add_column_if_missing;

-- 尽可能为历史图片补充独立所有者。方案归属比任务归属更可靠。
UPDATE aigenerationjobimages image
JOIN aigenerationjobs job ON job.JobId = image.JobId
SET image.UserID = job.UserID
WHERE image.UserID IS NULL
  AND job.UserID IS NOT NULL;

UPDATE aigenerationjobimages image
JOIN (
    SELECT pi.AiImageID, MIN(project.UserID) AS UserID
    FROM projectimages pi
    JOIN projects project ON project.ProjectID = pi.ProjectID
    WHERE pi.AiImageID IS NOT NULL
    GROUP BY pi.AiImageID
    HAVING COUNT(DISTINCT project.UserID) = 1
) owner ON owner.AiImageID = image.AiImageID
SET image.UserID = owner.UserID
WHERE image.UserID IS NULL;

UPDATE aigenerationjobimages image
SET image.RetentionStatus = 'retained',
    image.CleanupEligibleAt = NULL
WHERE EXISTS (
    SELECT 1
    FROM projectimages pi
    WHERE pi.AiImageID = image.AiImageID
)
OR EXISTS (
    SELECT 1
    FROM projects project
    WHERE project.CoverAiImageID = image.AiImageID
);

DELIMITER $$

DROP PROCEDURE IF EXISTS drop_job_image_fk $$
CREATE PROCEDURE drop_job_image_fk()
BEGIN
    DECLARE fk_name VARCHAR(64);

    SELECT kcu.CONSTRAINT_NAME
      INTO fk_name
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
    WHERE kcu.CONSTRAINT_SCHEMA = DATABASE()
      AND kcu.TABLE_NAME = 'aigenerationjobimages'
      AND kcu.COLUMN_NAME = 'JobId'
      AND kcu.REFERENCED_TABLE_NAME = 'aigenerationjobs'
    LIMIT 1;

    IF fk_name IS NOT NULL THEN
        SET @ddl = CONCAT(
            'ALTER TABLE aigenerationjobimages DROP FOREIGN KEY `',
            REPLACE(fk_name, '`', '``'),
            '`'
        );
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END $$

DELIMITER ;

CALL drop_job_image_fk();
DROP PROCEDURE IF EXISTS drop_job_image_fk;

ALTER TABLE aigenerationjobimages
    MODIFY COLUMN JobId varchar(50) DEFAULT NULL;

DELIMITER $$

DROP PROCEDURE IF EXISTS add_fk_if_missing $$
CREATE PROCEDURE add_fk_if_missing()
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
        WHERE CONSTRAINT_SCHEMA = DATABASE()
          AND TABLE_NAME = 'aigenerationjobimages'
          AND COLUMN_NAME = 'JobId'
          AND REFERENCED_TABLE_NAME = 'aigenerationjobs'
    ) THEN
        ALTER TABLE aigenerationjobimages
            ADD CONSTRAINT fk_aigenerationjobimages_jobid
            FOREIGN KEY (JobId) REFERENCES aigenerationjobs(JobId)
            ON DELETE SET NULL
            ON UPDATE CASCADE;
    END IF;
END $$

DELIMITER ;

CALL add_fk_if_missing();
DROP PROCEDURE IF EXISTS add_fk_if_missing;

DELIMITER $$

DROP PROCEDURE IF EXISTS add_index_if_missing $$
CREATE PROCEDURE add_index_if_missing()
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'aigenerationjobimages'
          AND INDEX_NAME = 'IX_AiGenerationJobImages_Cleanup'
    ) THEN
        ALTER TABLE aigenerationjobimages
            ADD INDEX IX_AiGenerationJobImages_Cleanup
            (RetentionStatus, CleanupEligibleAt);
    END IF;
END $$

DELIMITER ;

CALL add_index_if_missing();
DROP PROCEDURE IF EXISTS add_index_if_missing;
