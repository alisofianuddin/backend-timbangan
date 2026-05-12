using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;

        var totalBatches = await _db.Batches.CountAsync();
        var activeBatches = await _db.Batches.CountAsync(b => b.Status == "active");
        var totalMeasurements = await _db.Measurements.CountAsync(m => m.Status == "completed");
        var todayMeasurements = await _db.Measurements
            .CountAsync(m => m.Status == "completed" && m.CreatedAt != null && m.CreatedAt.Value.Date == today);

        var recentMeasurements = await _db.Measurements
            .Include(m => m.Batch)
            .Where(m => m.Status == "completed")
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .Select(m => new MeasurementDto
            {
                Id = m.Id,
                BatchId = m.BatchId,
                SamplingPoint = m.SamplingPoint,
                MeasurementNumber = m.MeasurementNumber,
                NetWeight = m.NetWeight,
                Status = m.Status,
                BatchNoBn = m.Batch != null ? m.Batch.NoBn : null,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        var recentBatches = await _db.Batches
            .Include(b => b.Creator)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .Select(b => new BatchDto
            {
                Id = b.Id,
                NoBn = b.NoBn,
                Status = b.Status,
                Creator = b.Creator != null ? new UserDto
                {
                    Id = b.Creator.Id,
                    Name = b.Creator.Name,
                    Email = b.Creator.Email
                } : null,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        return Ok(new DashboardDto
        {
            TotalBatches = totalBatches,
            ActiveBatches = activeBatches,
            TotalMeasurements = totalMeasurements,
            TodayMeasurements = todayMeasurements,
            RecentMeasurements = recentMeasurements,
            RecentBatches = recentBatches
        });
    }
}
