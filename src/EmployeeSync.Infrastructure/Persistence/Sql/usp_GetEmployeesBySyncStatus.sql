/*
    dbo.usp_GetEmployeesBySyncStatus

    Operational report: which employees are pending / synced / failed, and why.
    Deployed by the EF Core migration "AddUspGetEmployeesBySyncStatus"; this file is the single source.

    Parameters (both optional, NULL = no filter):
        @SyncStatus  'pending' | 'synced' | 'failed'
        @Provider    'provider1' | 'provider2'

    Result set 1: one summary row per sync status.
    Result set 2: the matching employees, most recently attempted first.

    Example:
        EXEC dbo.usp_GetEmployeesBySyncStatus @SyncStatus = 'failed';
*/
CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeesBySyncStatus
    @SyncStatus varchar(20) = NULL,
    @Provider   varchar(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @SyncStatus IS NOT NULL AND @SyncStatus NOT IN ('pending', 'synced', 'failed')
        THROW 50001, 'Invalid @SyncStatus. Use pending, synced or failed.', 1;

    IF @Provider IS NOT NULL AND @Provider NOT IN ('provider1', 'provider2')
        THROW 50002, 'Invalid @Provider. Use provider1 or provider2.', 1;

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
