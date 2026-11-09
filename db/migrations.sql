IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109154114_InitialCreate'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] varchar(20) NOT NULL,
        [ProviderEmployeeId] nvarchar(64) NOT NULL,
        [TrackTikEmployeeId] nvarchar(85) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [PhoneNumber] nvarchar(32) NULL,
        [Position] nvarchar(100) NULL,
        [Department] nvarchar(100) NULL,
        [StartDate] date NULL,
        [Status] varchar(20) NOT NULL,
        [SyncStatus] varchar(20) NOT NULL,
        [LastError] nvarchar(2000) NULL,
        [Attempts] int NOT NULL,
        [LastAttemptAt] datetimeoffset NULL,
        [LastSyncedAt] datetimeoffset NULL,
        [ExistsInTrackTik] bit NOT NULL,
        [TrackTikRecordId] nvarchar(64) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109154114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_SyncStatus] ON [Employees] ([SyncStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109154114_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Employees_Provider_ProviderEmployeeId] ON [Employees] ([Provider], [ProviderEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109154114_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109154114_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109154603_AddUspGetEmployeesBySyncStatus'
)
BEGIN
    EXEC(N'/*
        dbo.usp_GetEmployeesBySyncStatus

        Operational report: which employees are pending / synced / failed, and why.
        Deployed by the EF Core migration "AddUspGetEmployeesBySyncStatus"; this file is the single source.

        Parameters (both optional, NULL = no filter):
            @SyncStatus  ''pending'' | ''synced'' | ''failed''
            @Provider    ''provider1'' | ''provider2''

        Result set 1: one summary row per sync status.
        Result set 2: the matching employees, most recently attempted first.

        Example:
            EXEC dbo.usp_GetEmployeesBySyncStatus @SyncStatus = ''failed'';
    */
    CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeesBySyncStatus
        @SyncStatus varchar(20) = NULL,
        @Provider   varchar(20) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @SyncStatus IS NOT NULL AND @SyncStatus NOT IN (''pending'', ''synced'', ''failed'')
            THROW 50001, ''Invalid @SyncStatus. Use pending, synced or failed.'', 1;

        IF @Provider IS NOT NULL AND @Provider NOT IN (''provider1'', ''provider2'')
            THROW 50002, ''Invalid @Provider. Use provider1 or provider2.'', 1;

        SELECT
            e.SyncStatus,
            COUNT(*)             AS EmployeeCount,
            SUM(e.Attempts)      AS TotalAttempts,
            MAX(e.LastAttemptAt) AS LastAttemptAt
        FROM dbo.Employees AS e
        WHERE (@SyncStatus IS NULL OR e.SyncStatus = @SyncStatus)
          AND (@Provider IS NULL OR e.Provider = @Provider)
        GROUP BY e.SyncStatus
        ORDER BY e.SyncStatus
        OPTION (RECOMPILE); -- optional filters: avoid a plan sniffed for one parameter combination

        SELECT
            e.Provider,
            e.ProviderEmployeeId,
            e.TrackTikEmployeeId,
            e.FirstName,
            e.LastName,
            e.Email,
            e.SyncStatus,
            e.Attempts,
            e.LastAttemptAt,
            e.LastSyncedAt,
            e.LastError
        FROM dbo.Employees AS e
        WHERE (@SyncStatus IS NULL OR e.SyncStatus = @SyncStatus)
          AND (@Provider IS NULL OR e.Provider = @Provider)
        ORDER BY e.LastAttemptAt DESC, e.Provider, e.ProviderEmployeeId
        OPTION (RECOMPILE);
    END
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109154603_AddUspGetEmployeesBySyncStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109154603_AddUspGetEmployeesBySyncStatus', N'10.0.14');
END;

COMMIT;
GO

