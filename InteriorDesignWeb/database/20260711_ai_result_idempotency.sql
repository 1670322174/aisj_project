-- AI 结果并发保存幂等键。
-- 同一任务、同一 Provider 输出只能保存一次；历史明确重复项转入待清理状态。
USE aisj_club;

DELIMITER $$

DROP PROCEDURE IF EXISTS add_column_if_missing $$
CREATE PROCEDURE add_column_if_missing()
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'aigenerationjobimages'
          AND COLUMN_NAME = 'OutputKey'
    ) THEN
        ALTER TABLE aigenerationjobimages
            ADD COLUMN OutputKey varchar(64) DEFAULT NULL COMMENT 'Provider输出幂等键';
    END IF;
END $$

DELIMITER ;

CALL add_column_if_missing();
DROP PROCEDURE IF EXISTS add_column_if_missing;

-- 只有具备明确节点和文件名的记录才能判定为同一 Provider 输出。
UPDATE aigenerationjobimages
SET OutputKey = LOWER(SHA2(CONCAT_WS(
    '|',
    JSON_UNQUOTE(JSON_EXTRACT(MetadataJson, '$.NodeId')),
    JSON_UNQUOTE(JSON_EXTRACT(MetadataJson, '$.FileName')),
    COALESCE(JSON_UNQUOTE(JSON_EXTRACT(MetadataJson, '$.Subfolder')), ''),
    COALESCE(JSON_UNQUOTE(JSON_EXTRACT(MetadataJson, '$.Type')), 'output')
), 256))
WHERE JobId IS NOT NULL
  AND MetadataJson IS NOT NULL
  AND JSON_UNQUOTE(JSON_EXTRACT(MetadataJson, '$.NodeId')) IS NOT NULL
  AND JSON_UNQUOTE(JSON_EXTRACT(MetadataJson, '$.FileName')) IS NOT NULL;

DROP TEMPORARY TABLE IF EXISTS duplicate_ai_outputs;
CREATE TEMPORARY TABLE duplicate_ai_outputs AS
SELECT image.AiImageID, keeper.KeeperImageID
FROM aigenerationjobimages image
JOIN (
    SELECT JobId, OutputKey, MIN(AiImageID) AS KeeperImageID
    FROM aigenerationjobimages
    WHERE JobId IS NOT NULL AND OutputKey IS NOT NULL
    GROUP BY JobId, OutputKey
    HAVING COUNT(*) > 1
) keeper
  ON keeper.JobId = image.JobId
 AND keeper.OutputKey = image.OutputKey
WHERE image.AiImageID <> keeper.KeeperImageID;

-- 未被方案或封面引用的历史副本不再作为任务结果展示，进入延迟清理。
UPDATE aigenerationjobimages image
JOIN duplicate_ai_outputs duplicate ON duplicate.AiImageID = image.AiImageID
LEFT JOIN projectimages relation ON relation.AiImageID = image.AiImageID
LEFT JOIN projects cover ON cover.CoverAiImageID = image.AiImageID
SET image.JobId = NULL,
    image.OutputKey = NULL,
    image.RetentionStatus = 'cleanup_pending',
    image.DetachedAt = UTC_TIMESTAMP(),
    image.CleanupEligibleAt = DATE_ADD(UTC_TIMESTAMP(), INTERVAL 7 DAY)
WHERE relation.RelationID IS NULL
  AND cover.ProjectID IS NULL;

-- 已被方案引用的历史副本不得解绑；清空幂等键以避免唯一索引冲突。
UPDATE aigenerationjobimages image
JOIN duplicate_ai_outputs duplicate ON duplicate.AiImageID = image.AiImageID
SET image.OutputKey = NULL
WHERE image.JobId IS NOT NULL;

DROP TEMPORARY TABLE IF EXISTS duplicate_ai_outputs;

DELIMITER $$

DROP PROCEDURE IF EXISTS add_index_if_missing $$
CREATE PROCEDURE add_index_if_missing()
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'aigenerationjobimages'
          AND INDEX_NAME = 'UX_AiGenerationJobImages_Job_OutputKey'
    ) THEN
        ALTER TABLE aigenerationjobimages
            ADD UNIQUE INDEX UX_AiGenerationJobImages_Job_OutputKey (JobId, OutputKey);
    END IF;
END $$

DELIMITER ;

CALL add_index_if_missing();
DROP PROCEDURE IF EXISTS add_index_if_missing;
