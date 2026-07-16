CREATE TABLE IF NOT EXISTS schema_migrations (
    MigrationName varchar(160) NOT NULL,
    Checksum char(64) NOT NULL,
    AppliedAt datetime NOT NULL,
    ApplyMode varchar(20) NOT NULL DEFAULT 'executed',
    PRIMARY KEY (MigrationName)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
