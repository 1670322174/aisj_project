-- Phase 3 assistant image attachments and visual analysis. Compatible with MySQL 5.7.
-- Run after 20260716_agent_runtime_phase2.sql.

CREATE TABLE IF NOT EXISTS assistantattachments (
    AttachmentID bigint NOT NULL AUTO_INCREMENT,
    ConversationID bigint NOT NULL,
    UserID int NOT NULL,
    MessageID bigint NULL,
    RoomID int NULL,
    FileName varchar(255) NOT NULL,
    ContentType varchar(100) NOT NULL,
    FileSize bigint NOT NULL,
    Width int NOT NULL,
    Height int NOT NULL,
    Sha256 varchar(64) NOT NULL,
    CosPath varchar(500) NOT NULL,
    ThumbnailPath varchar(500) NULL,
    Kind varchar(40) NOT NULL DEFAULT 'unclassified',
    VisionStatus varchar(20) NOT NULL DEFAULT 'pending',
    VisionError varchar(500) NULL,
    CreatedAt datetime NOT NULL,
    IsDeleted tinyint(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (AttachmentID),
    KEY IX_AssistantAttachments_Conversation_Deleted_Created (ConversationID, IsDeleted, CreatedAt),
    KEY IX_AssistantAttachments_User_Hash (UserID, Sha256),
    KEY IX_AssistantAttachments_Message (MessageID),
    KEY IX_AssistantAttachments_Room (RoomID),
    CONSTRAINT FK_AssistantAttachments_Conversation FOREIGN KEY (ConversationID) REFERENCES assistantconversations (ConversationID) ON DELETE CASCADE,
    CONSTRAINT FK_AssistantAttachments_Message FOREIGN KEY (MessageID) REFERENCES assistantmessages (MessageID) ON DELETE SET NULL,
    CONSTRAINT FK_AssistantAttachments_Room FOREIGN KEY (RoomID) REFERENCES projectrooms (RoomID) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
