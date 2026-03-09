using FraudAnalytics.Data;
using FraudAnalytics.Hubs;
using FraudAnalytics.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<FraudDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(3)));

builder.Services.AddSignalR();

builder.Services.AddHttpClient<IRiskScoringService, RiskScoringService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["PythonService:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromMilliseconds(500);
});

builder.Services.AddHostedService<TransactionIngestionWorker>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();

builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

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