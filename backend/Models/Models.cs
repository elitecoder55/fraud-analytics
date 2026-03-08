namespace FraudAnalytics.Models;

public class Transaction
{
    public Guid TransactionId { get; set; } = Guid.NewGuid();
    public string CardNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public decimal RiskScore { get; set; }
    public bool IsFlagged { get; set; }
    public string TransactionType { get; set; } = "PURCHASE";
    public string Currency { get; set; } = "USD";
}

public class FraudAlert
{
    public Guid AlertId { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AlertStatus Status { get; set; } = AlertStatus.Open;
    public string? ReviewedBy { get; set; }
}

public class MerchantProfile
{
    public string MerchantId { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsHighRiskCategory { get; set; }
    public decimal AverageTransactionAmount { get; set; }
}

public enum AlertStatus { Open, UnderReview, Resolved, FalsePositive }

public record TransactionDto(
    Guid TransactionId,
    decimal Amount,
    string MerchantName,
    string Location,
    DateTime Timestamp,
    double RiskScore,
    bool IsFlagged
);

public record AlertDto(
    Guid AlertId,
    Guid TransactionId,
    string Reason,
    double RiskScore,
    DateTime Timestamp
);

public record RiskScoreResponse(double RiskScore, string[] Flags);