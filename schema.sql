-- =============================================================================
-- FraudShield Database Schema
-- SQL Server / Azure SQL Database
-- Demonstrates: Indexes, Triggers, Stored Procedures, Views, Partitioning
-- =============================================================================

-- ── Create Database (run as sysadmin) ────────────────────────────────────────
-- CREATE DATABASE FraudAnalyticsDb;
-- GO

USE FraudAnalyticsDb;
GO

-- =============================================================================
-- TABLES
-- =============================================================================

CREATE TABLE MerchantProfiles (
    MerchantId          VARCHAR(20)     NOT NULL PRIMARY KEY,
    MerchantName        NVARCHAR(200)   NOT NULL,
    Category            NVARCHAR(100)   NOT NULL,
    Country             NCHAR(2)        NOT NULL DEFAULT 'US',
    IsHighRiskCategory  BIT             NOT NULL DEFAULT 0,
    AvgTransactionAmt   DECIMAL(18,2)   NOT NULL DEFAULT 0,
    CreatedAt           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Transactions (
    TransactionId       UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    CardNumber          VARCHAR(64)         NOT NULL,   -- SHA-256 hash, never plaintext
    Amount              DECIMAL(18,2)       NOT NULL,
    MerchantId          VARCHAR(20)         NOT NULL,
    MerchantName        NVARCHAR(200)       NOT NULL,
    Location            NVARCHAR(200)       NOT NULL,
    [Timestamp]         DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
    RiskScore           DECIMAL(5,4)        NOT NULL DEFAULT 0,
    IsFlagged           BIT                 NOT NULL DEFAULT 0,
    TransactionType     VARCHAR(20)         NOT NULL DEFAULT 'PURCHASE',
    Currency            CHAR(3)             NOT NULL DEFAULT 'USD',
    IsReviewed          BIT                 NOT NULL DEFAULT 0,
    CONSTRAINT FK_Transactions_Merchant FOREIGN KEY (MerchantId)
        REFERENCES MerchantProfiles(MerchantId)
);

CREATE TABLE FraudAlerts (
    AlertId         UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    TransactionId   UNIQUEIDENTIFIER    NOT NULL,
    Reason          NVARCHAR(500)       NOT NULL,
    RiskScore       DECIMAL(5,4)        NOT NULL,
    [Timestamp]     DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
    [Status]        VARCHAR(20)         NOT NULL DEFAULT 'Open',
    ReviewedBy      NVARCHAR(100)       NULL,
    ReviewedAt      DATETIME2           NULL,
    CONSTRAINT FK_Alerts_Transaction FOREIGN KEY (TransactionId)
        REFERENCES Transactions(TransactionId) ON DELETE CASCADE
);

-- Audit trail — immutable log of all status changes
CREATE TABLE AlertAuditLog (
    LogId           BIGINT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    AlertId         UNIQUEIDENTIFIER NOT NULL,
    OldStatus       VARCHAR(20)     NOT NULL,
    NewStatus       VARCHAR(20)     NOT NULL,
    ChangedBy       NVARCHAR(100)   NOT NULL DEFAULT SYSTEM_USER,
    ChangedAt       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- =============================================================================
-- INDEXES (Performance Engineering)
-- =============================================================================

-- Most common query pattern: all transactions for a card, sorted by time
CREATE NONCLUSTERED INDEX IX_Transactions_Card_Time
    ON Transactions (CardNumber, [Timestamp] DESC)
    INCLUDE (Amount, MerchantId, RiskScore, IsFlagged);

-- Partial index: only index high-risk rows (small, fast, covers fraud dashboard)
CREATE NONCLUSTERED INDEX IX_Transactions_HighRisk
    ON Transactions (RiskScore DESC, [Timestamp] DESC)
    WHERE RiskScore >= 0.7;

-- Merchant foreign key index (avoids table scans on JOINs)
CREATE NONCLUSTERED INDEX IX_Transactions_Merchant
    ON Transactions (MerchantId)
    INCLUDE ([Timestamp], Amount, RiskScore);

-- Alert lookup by transaction
CREATE NONCLUSTERED INDEX IX_Alerts_Transaction
    ON FraudAlerts (TransactionId)
    INCLUDE (Reason, RiskScore, [Status]);

-- Open alerts dashboard query
CREATE NONCLUSTERED INDEX IX_Alerts_OpenStatus
    ON FraudAlerts ([Status], [Timestamp] DESC)
    WHERE [Status] = 'Open';
GO

-- =============================================================================
-- TRIGGER: Audit alert status changes
-- =============================================================================

CREATE OR ALTER TRIGGER TR_FraudAlerts_AuditStatus
ON FraudAlerts
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Only fire when Status column changed
    IF NOT UPDATE([Status]) RETURN;

    INSERT INTO AlertAuditLog (AlertId, OldStatus, NewStatus, ChangedBy)
    SELECT
        i.AlertId,
        d.[Status]  AS OldStatus,
        i.[Status]  AS NewStatus,
        ISNULL(i.ReviewedBy, SYSTEM_USER)
    FROM inserted i
    JOIN deleted  d ON i.AlertId = d.AlertId
    WHERE i.[Status] <> d.[Status];
END;
GO

-- =============================================================================
-- TRIGGER: Auto-flag transactions when RiskScore threshold exceeded
-- =============================================================================

CREATE OR ALTER TRIGGER TR_Transactions_AutoFlag
ON Transactions
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Set IsFlagged = 1 for any newly inserted/updated rows with high risk
    UPDATE t
    SET t.IsFlagged = 1
    FROM Transactions t
    JOIN inserted i ON t.TransactionId = i.TransactionId
    WHERE i.RiskScore >= 0.7 AND t.IsFlagged = 0;
END;
GO

-- =============================================================================
-- STORED PROCEDURE: Get fraud velocity per card
-- Used by .NET service to detect rapid-fire transaction patterns
-- =============================================================================

CREATE OR ALTER PROCEDURE sp_GetCardVelocity
    @CardNumber     VARCHAR(64),
    @WindowMinutes  INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @WindowStart DATETIME2 = DATEADD(MINUTE, -@WindowMinutes, SYSUTCDATETIME());

    SELECT
        COUNT(*)                AS TransactionCount,
        SUM(Amount)             AS TotalAmount,
        MAX(Amount)             AS MaxAmount,
        COUNT(DISTINCT Location) AS UniqueLocations,
        MIN([Timestamp])        AS FirstTransaction,
        MAX([Timestamp])        AS LastTransaction
    FROM Transactions
    WHERE CardNumber = @CardNumber
      AND [Timestamp] >= @WindowStart;
END;
GO

-- =============================================================================
-- STORED PROCEDURE: Fraud summary report (called by management dashboard)
-- =============================================================================

CREATE OR ALTER PROCEDURE sp_FraudSummaryReport
    @StartDate  DATETIME2,
    @EndDate    DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    -- Overall stats
    SELECT
        COUNT(*)                                        AS TotalTransactions,
        SUM(CASE WHEN IsFlagged = 1 THEN 1 ELSE 0 END) AS FlaggedCount,
        SUM(Amount)                                     AS TotalVolume,
        SUM(CASE WHEN IsFlagged = 1 THEN Amount ELSE 0 END) AS FraudulentVolume,
        AVG(CAST(RiskScore AS FLOAT))                   AS AvgRiskScore,
        MAX(RiskScore)                                  AS MaxRiskScore
    FROM Transactions
    WHERE [Timestamp] BETWEEN @StartDate AND @EndDate;

    -- Top 10 high-risk merchants
    SELECT TOP 10
        t.MerchantName,
        COUNT(*)            AS TxCount,
        SUM(t.Amount)       AS Volume,
        AVG(CAST(t.RiskScore AS FLOAT)) AS AvgRisk
    FROM Transactions t
    WHERE t.[Timestamp] BETWEEN @StartDate AND @EndDate
      AND t.IsFlagged = 1
    GROUP BY t.MerchantName
    ORDER BY AVG(CAST(t.RiskScore AS FLOAT)) DESC;

    -- Hourly distribution of fraud
    SELECT
        DATEPART(HOUR, [Timestamp]) AS HourOfDay,
        COUNT(*)                    AS FraudCount,
        AVG(CAST(RiskScore AS FLOAT)) AS AvgRisk
    FROM Transactions
    WHERE [Timestamp] BETWEEN @StartDate AND @EndDate
      AND IsFlagged = 1
    GROUP BY DATEPART(HOUR, [Timestamp])
    ORDER BY HourOfDay;
END;
GO

-- =============================================================================
-- VIEW: Real-time fraud dashboard (used by reporting layer)
-- =============================================================================

CREATE OR ALTER VIEW vw_RealtimeFraudDashboard
AS
SELECT
    t.TransactionId,
    t.Amount,
    t.MerchantName,
    t.Location,
    t.[Timestamp],
    t.RiskScore,
    t.IsFlagged,
    t.TransactionType,
    fa.Reason       AS AlertReason,
    fa.[Status]     AS AlertStatus,
    m.Category      AS MerchantCategory,
    m.IsHighRiskCategory
FROM Transactions t
LEFT JOIN FraudAlerts  fa ON t.TransactionId = fa.TransactionId
LEFT JOIN MerchantProfiles m ON t.MerchantId = m.MerchantId
WHERE t.[Timestamp] >= DATEADD(HOUR, -24, SYSUTCDATETIME());
GO

-- =============================================================================
-- SEED DATA: Merchant profiles
-- =============================================================================

INSERT INTO MerchantProfiles (MerchantId, MerchantName, Category, Country, IsHighRiskCategory, AvgTransactionAmt)
VALUES
    ('M100', 'Amazon',          'E-Commerce',       'US', 0, 75.00),
    ('M101', 'Walmart',         'Retail',           'US', 0, 55.00),
    ('M102', 'Shell Gas',       'Fuel',             'US', 0, 45.00),
    ('M103', 'McDonalds',       'Food & Beverage',  'US', 0, 12.00),
    ('M104', 'Apple Store',     'Electronics',      'US', 0, 350.00),
    ('M105', 'Local Cafe',      'Food & Beverage',  'US', 0, 8.00),
    ('M190', 'Crypto Exchange', 'Cryptocurrency',   'US', 1, 2500.00),
    ('M191', 'Overseas Casino', 'Gambling',         'XX', 1, 800.00),
    ('M192', 'Unknown Vendor',  'Unclassified',     'XX', 1, 1200.00);
GO
