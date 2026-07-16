USE aisj_club;

CREATE TABLE IF NOT EXISTS usersessions (
    UserSessionID bigint NOT NULL AUTO_INCREMENT,
    UserID int NOT NULL,
    TokenHash varchar(64) NOT NULL,
    ReplacedByTokenHash varchar(64) NULL,
    CreatedAt datetime NOT NULL,
    ExpiresAt datetime NOT NULL,
    LastUsedAt datetime NULL,
    RevokedAt datetime NULL,
    UserAgent varchar(300) NULL,
    IpAddress varchar(64) NULL,
    PRIMARY KEY (UserSessionID),
    UNIQUE KEY UX_UserSessions_TokenHash (TokenHash),
    KEY IX_UserSessions_User_Expiry (UserID, ExpiresAt, RevokedAt),
    CONSTRAINT FK_UserSessions_Users_UserID
        FOREIGN KEY (UserID) REFERENCES users (UserID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
