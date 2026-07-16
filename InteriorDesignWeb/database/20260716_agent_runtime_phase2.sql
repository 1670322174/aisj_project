-- Phase 2 multi-Agent runtime persistence. Compatible with MySQL 5.7.

CREATE TABLE IF NOT EXISTS assistantagentruns (
    RunID bigint NOT NULL AUTO_INCREMENT,
    ConversationID bigint NOT NULL,
    UserID int NOT NULL,
    ClientRequestID varchar(64) NOT NULL,
    Status varchar(20) NOT NULL DEFAULT 'running',
    EntryAgentID varchar(50) NOT NULL DEFAULT 'orchestrator',
    CurrentAgentID varchar(50) NULL,
    CurrentStage varchar(50) NULL,
    ModelCallCount int NOT NULL DEFAULT 0,
    HandoffCount int NOT NULL DEFAULT 0,
    InputTokens int NOT NULL DEFAULT 0,
    OutputTokens int NOT NULL DEFAULT 0,
    DurationMs int NOT NULL DEFAULT 0,
    ErrorCode varchar(50) NULL,
    ErrorMessage varchar(500) NULL,
    StartedAt datetime NOT NULL,
    CompletedAt datetime NULL,
    PRIMARY KEY (RunID),
    UNIQUE KEY UX_AssistantAgentRuns_Conversation_Request (ConversationID, ClientRequestID),
    KEY IX_AssistantAgentRuns_User_Started (UserID, StartedAt),
    CONSTRAINT FK_AssistantAgentRuns_Conversation FOREIGN KEY (ConversationID) REFERENCES assistantconversations (ConversationID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS assistantagentevents (
    EventID bigint NOT NULL AUTO_INCREMENT,
    RunID bigint NOT NULL,
    Sequence int NOT NULL,
    AgentID varchar(50) NOT NULL,
    EventType varchar(40) NOT NULL,
    Stage varchar(50) NULL,
    Title varchar(120) NOT NULL,
    Detail varchar(500) NULL,
    DataJson json NULL,
    CreatedAt datetime NOT NULL,
    PRIMARY KEY (EventID),
    UNIQUE KEY UX_AssistantAgentEvents_Run_Sequence (RunID, Sequence),
    CONSTRAINT FK_AssistantAgentEvents_Run FOREIGN KEY (RunID) REFERENCES assistantagentruns (RunID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS assistantagentartifacts (
    ArtifactID bigint NOT NULL AUTO_INCREMENT,
    ConversationID bigint NOT NULL,
    RunID bigint NOT NULL,
    AgentID varchar(50) NOT NULL,
    ArtifactType varchar(40) NOT NULL,
    Version int NOT NULL DEFAULT 1,
    Status varchar(20) NOT NULL DEFAULT 'draft',
    Title varchar(160) NULL,
    ContentJson longtext NOT NULL,
    CreatedAt datetime NOT NULL,
    PRIMARY KEY (ArtifactID),
    KEY IX_AssistantAgentArtifacts_Conversation_Type_Version (ConversationID, ArtifactType, Version),
    KEY IX_AssistantAgentArtifacts_Run (RunID),
    CONSTRAINT FK_AssistantAgentArtifacts_Run FOREIGN KEY (RunID) REFERENCES assistantagentruns (RunID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
