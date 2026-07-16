-- 作用：为 AIJob 任务中心、7 个工作流追踪和 AI 结果入库补齐数据库字段。
-- 使用前请先备份数据库。MySQL 5.7 不支持 ALTER TABLE ADD COLUMN IF NOT EXISTS，本脚本用存储过程做条件判断。

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

-- AI 任务表增强
CALL add_column_if_missing('aigenerationjobs', 'UserID', 'ALTER TABLE aigenerationjobs ADD COLUMN UserID int(11) DEFAULT NULL COMMENT ''生成用户ID''');
CALL add_column_if_missing('aigenerationjobs', 'WorkflowCode', 'ALTER TABLE aigenerationjobs ADD COLUMN WorkflowCode varchar(50) NOT NULL DEFAULT ''text_to_image'' COMMENT ''工作流编码''');
CALL add_column_if_missing('aigenerationjobs', 'ModelCode', 'ALTER TABLE aigenerationjobs ADD COLUMN ModelCode varchar(100) DEFAULT NULL COMMENT ''模型编码''');
CALL add_column_if_missing('aigenerationjobs', 'ProviderType', 'ALTER TABLE aigenerationjobs ADD COLUMN ProviderType varchar(30) NOT NULL DEFAULT ''ComfyUI'' COMMENT ''执行来源''');
CALL add_column_if_missing('aigenerationjobs', 'ProviderJobId', 'ALTER TABLE aigenerationjobs ADD COLUMN ProviderJobId varchar(100) DEFAULT NULL COMMENT ''ComfyUI prompt_id''');
CALL add_column_if_missing('aigenerationjobs', 'Prompt', 'ALTER TABLE aigenerationjobs ADD COLUMN Prompt text DEFAULT NULL COMMENT ''正向提示词''');
CALL add_column_if_missing('aigenerationjobs', 'NegativePrompt', 'ALTER TABLE aigenerationjobs ADD COLUMN NegativePrompt text DEFAULT NULL COMMENT ''反向提示词''');
CALL add_column_if_missing('aigenerationjobs', 'InputJson', 'ALTER TABLE aigenerationjobs ADD COLUMN InputJson longtext DEFAULT NULL COMMENT ''完整输入参数''');
CALL add_column_if_missing('aigenerationjobs', 'OutputJson', 'ALTER TABLE aigenerationjobs ADD COLUMN OutputJson longtext DEFAULT NULL COMMENT ''完整输出结果''');
CALL add_column_if_missing('aigenerationjobs', 'ErrorCode', 'ALTER TABLE aigenerationjobs ADD COLUMN ErrorCode varchar(50) DEFAULT NULL COMMENT ''错误码''');
CALL add_column_if_missing('aigenerationjobs', 'ErrorMessage', 'ALTER TABLE aigenerationjobs ADD COLUMN ErrorMessage text DEFAULT NULL COMMENT ''错误信息''');
CALL add_column_if_missing('aigenerationjobs', 'StartedAt', 'ALTER TABLE aigenerationjobs ADD COLUMN StartedAt datetime DEFAULT NULL COMMENT ''开始执行时间''');
CALL add_column_if_missing('aigenerationjobs', 'CompletedAt', 'ALTER TABLE aigenerationjobs ADD COLUMN CompletedAt datetime DEFAULT NULL COMMENT ''完成时间''');
CALL add_column_if_missing('aigenerationjobs', 'ProgressValue', 'ALTER TABLE aigenerationjobs ADD COLUMN ProgressValue int(11) NOT NULL DEFAULT 0 COMMENT ''数字进度0-100''');
CALL add_column_if_missing('aigenerationjobs', 'CostUnits', 'ALTER TABLE aigenerationjobs ADD COLUMN CostUnits int(11) NOT NULL DEFAULT 1 COMMENT ''本次任务消耗额度单位''');

-- AI 结果图表增强
CALL add_column_if_missing('aigenerationjobimages', 'UserID', 'ALTER TABLE aigenerationjobimages ADD COLUMN UserID int(11) DEFAULT NULL COMMENT ''生成用户ID''');
CALL add_column_if_missing('aigenerationjobimages', 'WorkflowCode', 'ALTER TABLE aigenerationjobimages ADD COLUMN WorkflowCode varchar(50) NOT NULL DEFAULT ''text_to_image'' COMMENT ''工作流编码''');
CALL add_column_if_missing('aigenerationjobimages', 'ModelCode', 'ALTER TABLE aigenerationjobimages ADD COLUMN ModelCode varchar(100) DEFAULT NULL COMMENT ''模型编码''');
CALL add_column_if_missing('aigenerationjobimages', 'Prompt', 'ALTER TABLE aigenerationjobimages ADD COLUMN Prompt text DEFAULT NULL COMMENT ''生成提示词''');
CALL add_column_if_missing('aigenerationjobimages', 'SourceType', 'ALTER TABLE aigenerationjobimages ADD COLUMN SourceType varchar(30) NOT NULL DEFAULT ''ai'' COMMENT ''来源类型：image/video''');
CALL add_column_if_missing('aigenerationjobimages', 'Style', 'ALTER TABLE aigenerationjobimages ADD COLUMN Style varchar(50) DEFAULT NULL COMMENT ''风格''');
CALL add_column_if_missing('aigenerationjobimages', 'RoomType', 'ALTER TABLE aigenerationjobimages ADD COLUMN RoomType varchar(50) DEFAULT NULL COMMENT ''房间类型''');
CALL add_column_if_missing('aigenerationjobimages', 'Tags', 'ALTER TABLE aigenerationjobimages ADD COLUMN Tags varchar(1000) DEFAULT NULL COMMENT ''标签''');
CALL add_column_if_missing('aigenerationjobimages', 'MetadataJson', 'ALTER TABLE aigenerationjobimages ADD COLUMN MetadataJson json DEFAULT NULL COMMENT ''生成元数据''');

-- 项目图片关联表增强
CALL add_column_if_missing('projectimages', 'SourceType', 'ALTER TABLE projectimages ADD COLUMN SourceType varchar(30) NOT NULL DEFAULT ''gallery'' COMMENT ''gallery/upload/ai''');
CALL add_column_if_missing('projectimages', 'SortOrder', 'ALTER TABLE projectimages ADD COLUMN SortOrder int(11) NOT NULL DEFAULT 0 COMMENT ''排序''');
CALL add_column_if_missing('projectimages', 'IsFavorite', 'ALTER TABLE projectimages ADD COLUMN IsFavorite tinyint(1) NOT NULL DEFAULT 0 COMMENT ''是否收藏''');
CALL add_column_if_missing('projectimages', 'IsCover', 'ALTER TABLE projectimages ADD COLUMN IsCover tinyint(1) NOT NULL DEFAULT 0 COMMENT ''是否封面''');
CALL add_column_if_missing('projectimages', 'Note', 'ALTER TABLE projectimages ADD COLUMN Note text DEFAULT NULL COMMENT ''设计备注''');
CALL add_column_if_missing('projectimages', 'CreatedByUserID', 'ALTER TABLE projectimages ADD COLUMN CreatedByUserID int(11) DEFAULT NULL COMMENT ''加入项目的用户''');

-- 兼容旧数据
UPDATE aigenerationjobs SET ProviderJobId = PromptId WHERE ProviderJobId IS NULL;
UPDATE aigenerationjobs SET InputJson = ParametersJson WHERE InputJson IS NULL;
UPDATE aigenerationjobs SET OutputJson = GeneratedImagesJson WHERE OutputJson IS NULL AND GeneratedImagesJson IS NOT NULL;
UPDATE projectimages SET SourceType = 'ai' WHERE AiImageID IS NOT NULL;
UPDATE projectimages SET SourceType = 'gallery' WHERE AiImageID IS NULL AND ImageID IS NOT NULL;

DROP PROCEDURE IF EXISTS add_column_if_missing;

-- 项目管理字段增强：如果你的数据库已执行过这些 ALTER，重复执行不会再新增字段。
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

CALL add_column_if_missing('projects', 'Status', 'ALTER TABLE projects ADD COLUMN Status varchar(30) NOT NULL DEFAULT ''draft'' COMMENT ''draft/designing/reviewing/completed/archived''');
CALL add_column_if_missing('projects', 'UpdatedAt', 'ALTER TABLE projects ADD COLUMN UpdatedAt datetime DEFAULT NULL COMMENT ''更新时间''');
CALL add_column_if_missing('projects', 'CoverImageID', 'ALTER TABLE projects ADD COLUMN CoverImageID int(11) DEFAULT NULL COMMENT ''普通图片封面''');
CALL add_column_if_missing('projects', 'CoverAiImageID', 'ALTER TABLE projects ADD COLUMN CoverAiImageID int(11) DEFAULT NULL COMMENT ''AI图片封面''');
CALL add_column_if_missing('projects', 'Style', 'ALTER TABLE projects ADD COLUMN Style varchar(50) DEFAULT NULL COMMENT ''项目风格''');
CALL add_column_if_missing('projects', 'HouseType', 'ALTER TABLE projects ADD COLUMN HouseType varchar(50) DEFAULT NULL COMMENT ''户型''');
CALL add_column_if_missing('projects', 'Area', 'ALTER TABLE projects ADD COLUMN Area decimal(10,2) DEFAULT NULL COMMENT ''面积''');
CALL add_column_if_missing('projects', 'Tags', 'ALTER TABLE projects ADD COLUMN Tags varchar(1000) DEFAULT NULL COMMENT ''项目标签''');
CALL add_column_if_missing('projects', 'IsDeleted', 'ALTER TABLE projects ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 COMMENT ''软删除''');
CALL add_column_if_missing('projects', 'DeletedAt', 'ALTER TABLE projects ADD COLUMN DeletedAt datetime DEFAULT NULL COMMENT ''删除时间''');
UPDATE projects SET UpdatedAt = CreatedAt WHERE UpdatedAt IS NULL;

CALL add_column_if_missing('projectrooms', 'RoomType', 'ALTER TABLE projectrooms ADD COLUMN RoomType varchar(50) DEFAULT NULL COMMENT ''标准房间类型''');
CALL add_column_if_missing('projectrooms', 'Style', 'ALTER TABLE projectrooms ADD COLUMN Style varchar(50) DEFAULT NULL COMMENT ''房间风格''');
CALL add_column_if_missing('projectrooms', 'Area', 'ALTER TABLE projectrooms ADD COLUMN Area decimal(10,2) DEFAULT NULL COMMENT ''房间面积''');
CALL add_column_if_missing('projectrooms', 'Requirement', 'ALTER TABLE projectrooms ADD COLUMN Requirement text DEFAULT NULL COMMENT ''房间设计需求''');
CALL add_column_if_missing('projectrooms', 'Status', 'ALTER TABLE projectrooms ADD COLUMN Status varchar(30) NOT NULL DEFAULT ''not_started'' COMMENT ''not_started/designing/completed''');
CALL add_column_if_missing('projectrooms', 'UpdatedAt', 'ALTER TABLE projectrooms ADD COLUMN UpdatedAt datetime DEFAULT NULL COMMENT ''更新时间''');
UPDATE projectrooms SET RoomType = Type WHERE RoomType IS NULL AND Type IS NOT NULL;

DROP PROCEDURE IF EXISTS add_column_if_missing;

-- 新增观察表：不存在才创建。
CREATE TABLE IF NOT EXISTS userquotas (
  QuotaID int(11) NOT NULL AUTO_INCREMENT,
  UserID int(11) NOT NULL,
  TotalUnits int(11) NOT NULL DEFAULT 0,
  UsedUnits int(11) NOT NULL DEFAULT 0,
  RemainingUnits int(11) NOT NULL DEFAULT 0,
  MonthlyLimit int(11) DEFAULT NULL,
  MonthlyUsed int(11) NOT NULL DEFAULT 0,
  LastResetAt datetime DEFAULT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime DEFAULT NULL,
  PRIMARY KEY (QuotaID),
  UNIQUE KEY UX_UserQuotas_UserID (UserID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='用户额度汇总表';

CREATE TABLE IF NOT EXISTS usagerecords (
  UsageID bigint(20) NOT NULL AUTO_INCREMENT,
  UserID int(11) DEFAULT NULL,
  JobId varchar(50) DEFAULT NULL,
  UsageType varchar(50) NOT NULL DEFAULT 'ai_generation',
  WorkflowCode varchar(50) DEFAULT NULL,
  ModelCode varchar(100) DEFAULT NULL,
  ProviderType varchar(30) DEFAULT 'ComfyUI',
  Units int(11) NOT NULL DEFAULT 1,
  Status varchar(30) NOT NULL DEFAULT 'created',
  RequestJson json DEFAULT NULL,
  ResultJson json DEFAULT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (UsageID),
  KEY IX_UsageRecords_UserID (UserID),
  KEY IX_UsageRecords_JobId (JobId),
  KEY IX_UsageRecords_CreatedAt (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='使用量记录表';

CREATE TABLE IF NOT EXISTS projectactivities (
  ActivityID bigint(20) NOT NULL AUTO_INCREMENT,
  ProjectID int(11) NOT NULL,
  UserID int(11) DEFAULT NULL,
  ActivityType varchar(50) NOT NULL,
  Title varchar(200) DEFAULT NULL,
  Content text DEFAULT NULL,
  MetadataJson json DEFAULT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (ActivityID),
  KEY IX_ProjectActivities_ProjectID (ProjectID),
  KEY IX_ProjectActivities_UserID (UserID),
  KEY IX_ProjectActivities_CreatedAt (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='项目动态记录表';
