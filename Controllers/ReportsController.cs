using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int? batchId, [FromQuery] int? samplingPoint)
    {
        var batches = await _db.Batches
            .Include(b => b.Creator)
            .Include(b => b.Measurements)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BatchDto
            {
                Id = b.Id,
                NoBn = b.NoBn,
                Status = b.Status,
                CompletedCount = b.Measurements.Count(m => m.Status == "completed"),
                Creator = b.Creator != null ? new UserDto
                {
                    Id = b.Creator.Id,
                    Name = b.Creator.Name,
                    Email = b.Creator.Email
                } : null,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        BatchDto? selectedBatch = null;
        List<MeasurementDto> measurements = new();
        ReportStatsDto? stats = null;

        if (batchId.HasValue)
        {
            var batch = await _db.Batches.Include(b => b.Creator).FirstOrDefaultAsync(b => b.Id == batchId.Value);
            if (batch != null)
            {
                selectedBatch = new BatchDto
                {
                    Id = batch.Id,
                    NoBn = batch.NoBn,
                    Status = batch.Status,
                    Creator = batch.Creator != null ? new UserDto
                    {
                        Id = batch.Creator.Id,
                        Name = batch.Creator.Name,
                        Email = batch.Creator.Email
                    } : null,
                    CreatedAt = batch.CreatedAt
                };

                var query = _db.Measurements
                    .Where(m => m.BatchId == batchId.Value && m.Status == "completed");

                if (samplingPoint.HasValue)
                    query = query.Where(m => m.SamplingPoint == samplingPoint.Value);

                var measurementEntities = await query
                    .OrderBy(m => m.SamplingPoint)
                    .ThenBy(m => m.MeasurementNumber)
                    .ToListAsync();

                measurements = measurementEntities.Select(m => new MeasurementDto
                {
                    Id = m.Id,
                    BatchId = m.BatchId,
                    SamplingPoint = m.SamplingPoint,
                    MeasurementNumber = m.MeasurementNumber,
                    TareReadings = m.TareReadings,
                    TareAverage = m.TareAverage,
                    TareMin = m.TareMin,
                    TareMax = m.TareMax,
                    GrossWeight = m.GrossWeight,
                    NetWeight = m.NetWeight,
                    PassFail = m.PassFail,
                    Unit = m.Unit,
                    Status = m.Status,
                    TareCount = m.TareCount,
                    CreatedAt = m.CreatedAt
                }).ToList();

                if (measurements.Count > 0)
                {
                    var netWeights = measurementEntities
                        .Where(m => m.NetWeight.HasValue)
                        .Select(m => m.NetWeight!.Value)
                        .ToList();

                    stats = new ReportStatsDto
                    {
                        Count = measurements.Count,
                        AvgNet = Math.Round(netWeights.Average(), 4),
                        MinNet = Math.Round(netWeights.Min(), 4),
                        MaxNet = Math.Round(netWeights.Max(), 4),
                        StdDev = Math.Round(StdDev(netWeights), 4),
                        AvgTare = Math.Round(measurementEntities.Where(m => m.TareAverage.HasValue).Average(m => m.TareAverage!.Value), 4),
                        AvgGross = Math.Round(measurementEntities.Where(m => m.GrossWeight.HasValue).Average(m => m.GrossWeight!.Value), 4)
                    };
                }
            }
        }

        return Ok(new ReportDto
        {
            Batches = batches,
            SelectedBatch = selectedBatch,
            Measurements = measurements,
            Stats = stats
        });
    }

    private static double StdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquaredDiffs / (values.Count - 1));
    }
}
