-- Phase 1 AI design assistant persistence. The selected connection determines
-- the target database; no database name is hard-coded here.

CREATE TABLE IF NOT EXISTS assistantconversations (
    ConversationID bigint NOT NULL AUTO_INCREMENT,
    UserID int NOT NULL,
    ProjectID int NULL,
    RoomID int NULL,
    Title varchar(120) NOT NULL DEFAULT '新设计对话',
    Status varchar(20) NOT NULL DEFAULT 'active',
    CurrentBriefJson json NULL,
    CreatedAt datetime NOT NULL,
    UpdatedAt datetime NOT NULL,
    IsDeleted tinyint(1) NOT NULL DEFAULT 0,
    DeletedAt datetime NULL,
    PRIMARY KEY (ConversationID),
    KEY IX_AssistantConversations_User_Deleted_Updated (UserID, IsDeleted, UpdatedAt),
    KEY IX_AssistantConversations_Project (ProjectID),
    KEY IX_AssistantConversations_Room (RoomID),
    CONSTRAINT FK_AssistantConversations_User FOREIGN KEY (UserID) REFERENCES users (UserID) ON DELETE CASCADE,
    CONSTRAINT FK_AssistantConversations_Project FOREIGN KEY (ProjectID) REFERENCES projects (ProjectID) ON DELETE SET NULL,
    CONSTRAINT FK_AssistantConversations_Room FOREIGN KEY (RoomID) REFERENCES projectrooms (RoomID) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS assistantmessages (
    MessageID bigint NOT NULL AUTO_INCREMENT,
    ConversationID bigint NOT NULL,
    Role varchar(20) NOT NULL,
    Content longtext NOT NULL,
    StructuredDataJson json NULL,
    ModelCode varchar(100) NULL,
    InputTokens int NOT NULL DEFAULT 0,
    OutputTokens int NOT NULL DEFAULT 0,
    DurationMs int NOT NULL DEFAULT 0,
    ClientRequestID varchar(64) NULL,
    ErrorMessage varchar(500) NULL,
    CreatedAt datetime NOT NULL,
    PRIMARY KEY (MessageID),
    UNIQUE KEY UX_AssistantMessages_Conversation_ClientRequest (ConversationID, ClientRequestID),
    KEY IX_AssistantMessages_Conversation_Created (ConversationID, CreatedAt),
    CONSTRAINT FK_AssistantMessages_Conversation FOREIGN KEY (ConversationID) REFERENCES assistantconversations (ConversationID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS assistantgenerationactions (
    ActionID bigint NOT NULL AUTO_INCREMENT,
    ConversationID bigint NOT NULL,
    MessageID bigint NULL,
    JobID varchar(50) NULL,
    ProjectID int NULL,
    RoomID int NULL,
    GenerationType varchar(30) NOT NULL DEFAULT 'text_to_image',
    WorkflowCode varchar(50) NULL,
    Prompt longtext NOT NULL,
    NegativePrompt longtext NULL,
    ParametersJson longtext NOT NULL,
    Status varchar(20) NOT NULL DEFAULT 'proposed',
    IdempotencyKey varchar(64) NOT NULL,
    AutoAddToProject tinyint(1) NOT NULL DEFAULT 1,
    CreatedAt datetime NOT NULL,
    ExecutedAt datetime NULL,
    ErrorMessage varchar(500) NULL,
    PRIMARY KEY (ActionID),
    UNIQUE KEY UX_AssistantActions_Conversation_Idempotency (ConversationID, IdempotencyKey),
    KEY IX_AssistantActions_Job (JobID),
    KEY IX_AssistantActions_Message (MessageID),
    KEY IX_AssistantActions_Project (ProjectID),
    KEY IX_AssistantActions_Room (RoomID),
    CONSTRAINT FK_AssistantActions_Conversation FOREIGN KEY (ConversationID) REFERENCES assistantconversations (ConversationID) ON DELETE CASCADE,
    CONSTRAINT FK_AssistantActions_Message FOREIGN KEY (MessageID) REFERENCES assistantmessages (MessageID) ON DELETE SET NULL,
    CONSTRAINT FK_AssistantActions_Project FOREIGN KEY (ProjectID) REFERENCES projects (ProjectID) ON DELETE SET NULL,
    CONSTRAINT FK_AssistantActions_Room FOREIGN KEY (RoomID) REFERENCES projectrooms (RoomID) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
