using FraudAnalytics.Data;
using FraudAnalytics.Hubs;
using FraudAnalytics.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework Core → Azure SQL (or local SQL Server)
builder.Services.AddDbContext<FraudDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(3)));

// SignalR for real-time push
builder.Services.AddSignalR();

// HttpClient for calling Python ML microservice
builder.Services.AddHttpClient<IRiskScoringService, RiskScoringService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PythonService:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromMilliseconds(500); // Hard SLA: 500ms
});

// Background worker that simulates incoming transactions
builder.Services.AddHostedService<TransactionIngestionWorker>();

builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();

builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FraudDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();

app.MapControllers();
app.MapHub<TransactionHub>("/hubs/transactions");

app.Run();
