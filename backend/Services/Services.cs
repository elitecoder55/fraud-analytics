using FraudAnalytics.Data;
using FraudAnalytics.Hubs;
using FraudAnalytics.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FraudAnalytics.Services;

public interface ITransactionService
{
    Task<TransactionDto> ProcessAsync(Transaction tx);
    Task<IEnumerable<TransactionDto>> GetRecentAsync(int count = 50);
    Task<IEnumerable<TransactionDto>> GetHighRiskAsync(DateTime from, DateTime to);
}

public interface IFraudDetectionService
{
    Task<bool> CheckAndAlertAsync(Transaction tx, double riskScore);
}

public interface IRiskScoringService
{
    Task<RiskScoreResponse> GetRiskScoreAsync(Transaction tx);
}

public class TransactionService : ITransactionService
{
    private readonly FraudDbContext _db;
    private readonly IRiskScoringService _riskScorer;
    private readonly IFraudDetectionService _fraudDetection;
    private readonly IHubContext<TransactionHub> _hub;

    public TransactionService(
        FraudDbContext db,
        IRiskScoringService riskScorer,
        IFraudDetectionService fraudDetection,
        IHubContext<TransactionHub> hub)
    {
        _db = db;
        _riskScorer = riskScorer;
        _fraudDetection = fraudDetection;
        _hub = hub;
    }

    public async Task<TransactionDto> ProcessAsync(Transaction tx)
    {
        var scoreResult = await _riskScorer.GetRiskScoreAsync(tx);
        tx.RiskScore = (decimal)scoreResult.RiskScore;
        tx.IsFlagged = scoreResult.RiskScore >= 0.7;

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();

        var dto = ToDto(tx);
        await _hub.Clients.Group("analysts").SendAsync("ReceiveTransaction", dto);

        if (tx.IsFlagged)
            await _fraudDetection.CheckAndAlertAsync(tx, scoreResult.RiskScore);

        return dto;
    }

    public async Task<IEnumerable<TransactionDto>> GetRecentAsync(int count = 50)
        => await _db.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .Select(t => ToDto(t))
            .ToListAsync();

    public async Task<IEnumerable<TransactionDto>> GetHighRiskAsync(DateTime from, DateTime to)
        => await _db.Transactions
            .AsNoTracking()
            .Where(t => t.RiskScore >= 0.7m && t.Timestamp >= from && t.Timestamp <= to)
            .OrderByDescending(t => t.RiskScore)
            .Select(t => ToDto(t))
            .ToListAsync();

    private static TransactionDto ToDto(Transaction t) => new(
        t.TransactionId, t.Amount, t.MerchantName,
        t.Location, t.Timestamp, (double)t.RiskScore, t.IsFlagged);
}

public class FraudDetectionService : IFraudDetectionService
{
    private readonly FraudDbContext _db;
    private readonly IHubContext<TransactionHub> _hub;

    public FraudDetectionService(FraudDbContext db, IHubContext<TransactionHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<bool> CheckAndAlertAsync(Transaction tx, double riskScore)
    {
        var recentCount = await _db.Transactions
            .CountAsync(t => t.CardNumber == tx.CardNumber
                          && t.Timestamp >= DateTime.UtcNow.AddMinutes(-5)
                          && t.TransactionId != tx.TransactionId);

        var reason = riskScore >= 0.9
            ? "Extremely high ML risk score"
            : recentCount >= 3
                ? $"Rapid-fire: {recentCount + 1} transactions in 5 minutes"
                : "High ML risk score — manual review required";

        var alert = new FraudAlert
        {
            TransactionId = tx.TransactionId,
            Reason = reason,
            RiskScore = (decimal)riskScore,
        };

        _db.FraudAlerts.Add(alert);
        await _db.SaveChangesAsync();

        var dto = new AlertDto(alert.AlertId, tx.TransactionId, reason, riskScore, alert.Timestamp);
        await _hub.Clients.Group("analysts").SendAsync("ReceiveAlert", dto);

        return true;
    }
}

public class RiskScoringService : IRiskScoringService
{
    private readonly HttpClient _http;
    private readonly ILogger<RiskScoringService> _logger;

    public RiskScoringService(HttpClient http, ILogger<RiskScoringService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<RiskScoreResponse> GetRiskScoreAsync(Transaction tx)
    {
        try
        {
            var payload = new
            {
                transaction_id = tx.TransactionId,
                amount = tx.Amount,
                merchant_id = tx.MerchantId,
                location = tx.Location,
                transaction_type = tx.TransactionType,
                hour_of_day = tx.Timestamp.Hour,
                day_of_week = (int)tx.Timestamp.DayOfWeek,
            };

            var response = await _http.PostAsJsonAsync("/predict", payload);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RiskScoreResponse>();
            return result ?? new RiskScoreResponse(0.0, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python service unavailable, using fallback score");
            var fallbackScore = tx.Amount > 5000 ? 0.85 : tx.Amount > 1000 ? 0.45 : 0.1;
            return new RiskScoreResponse(fallbackScore, new[] { "FALLBACK_RULES" });
        }
    }
}