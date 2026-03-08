namespace FraudAnalytics.Services;

public class TransactionIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TransactionIngestionWorker> _logger;

    private static readonly string[] Merchants = {
        "Amazon", "Walmart", "Shell Gas", "McDonalds", "Apple Store",
        "Unknown Vendor", "Crypto Exchange", "Overseas Casino", "Local Cafe"
    };

    private static readonly string[] Locations = {
        "New York, US", "London, UK", "Mumbai, IN", "Lagos, NG",
        "Unknown", "Singapore, SG", "Dubai, UAE", "Paris, FR"
    };

    public TransactionIngestionWorker(
        IServiceScopeFactory factory,
        ILogger<TransactionIngestionWorker> logger)
    {
        _scopeFactory = factory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rng = new Random();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var txService = scope.ServiceProvider
                    .GetRequiredService<ITransactionService>();

                var tx = new Models.Transaction
                {
                    CardNumber = $"CARD_{rng.Next(1000, 9999)}",
                    Amount = Math.Round((decimal)(rng.NextDouble() * 8000 + 1), 2),
                    MerchantId = $"M{rng.Next(100, 199)}",
                    MerchantName = Merchants[rng.Next(Merchants.Length)],
                    Location = Locations[rng.Next(Locations.Length)],
                    Timestamp = DateTime.UtcNow,
                };

                await txService.ProcessAsync(tx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting transaction");
            }

            await Task.Delay(rng.Next(300, 1000), stoppingToken);
        }
    }
}