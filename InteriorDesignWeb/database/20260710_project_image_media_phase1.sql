-- 第一阶段图片链路修复：清理重复方案图片并增加唯一约束。
-- 执行前请备份数据库。重复图片仅保留 RelationID 最小的一条。

USE aisj_club;

DELETE newer
FROM projectimages newer
JOIN projectimages older
  ON newer.ProjectID = older.ProjectID
 AND newer.ImageID = older.ImageID
 AND newer.ImageID IS NOT NULL
 AND newer.RelationID > older.RelationID;

DELETE newer
FROM projectimages newer
JOIN projectimages older
  ON newer.ProjectID = older.ProjectID
 AND newer.AiImageID = older.AiImageID
 AND newer.AiImageID IS NOT NULL
 AND newer.RelationID > older.RelationID;

DELIMITER $$

DROP PROCEDURE IF EXISTS add_unique_index_if_missing $$
CREATE PROCEDURE add_unique_index_if_missing(
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

CALL add_unique_index_if_missing(
    'projectimages',
    'UX_ProjectImages_Project_Image',
    'ALTER TABLE projectimages ADD UNIQUE INDEX UX_ProjectImages_Project_Image (ProjectID, ImageID)'
);

CALL add_unique_index_if_missing(
    'projectimages',
    'UX_ProjectImages_Project_AiImage',
    'ALTER TABLE projectimages ADD UNIQUE INDEX UX_ProjectImages_Project_AiImage (ProjectID, AiImageID)'
);

DROP PROCEDURE IF EXISTS add_unique_index_if_missing;

