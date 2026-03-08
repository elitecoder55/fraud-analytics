using FraudAnalytics.Data;
using FraudAnalytics.Models;
using FraudAnalytics.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FraudAnalytics.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _txService;
    private readonly FraudDbContext _db;

    public TransactionsController(ITransactionService txService, FraudDbContext db)
    {
        _txService = txService;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] Transaction tx)
    {
        var dto = await _txService.ProcessAsync(tx);
        return CreatedAtAction(nameof(GetById), new { id = dto.TransactionId }, dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tx = await _db.Transactions.FindAsync(id);
        return tx is null ? NotFound() : Ok(tx);
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int count = 50)
        => Ok(await _txService.GetRecentAsync(Math.Min(count, 500)));

    [HttpGet("high-risk")]
    public async Task<IActionResult> HighRisk(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var start = from ?? DateTime.UtcNow.AddHours(-24);
        var end = to ?? DateTime.UtcNow;
        return Ok(await _txService.GetHighRiskAsync(start, end));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var stats = await _db.Transactions
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Flagged = g.Count(t => t.IsFlagged),
                AvgRiskScore = g.Average(t => t.RiskScore),
                TotalVolume = g.Sum(t => t.Amount),
            })
            .FirstOrDefaultAsync();

        return Ok(stats);
    }
}

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly FraudDbContext _db;

    public AlertsController(FraudDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var alerts = await _db.FraudAlerts
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Take(200)
            .ToListAsync();
        return Ok(alerts);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AlertStatus newStatus)
    {
        var alert = await _db.FraudAlerts.FindAsync(id);
        if (alert is null) return NotFound();
        alert.Status = newStatus;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}