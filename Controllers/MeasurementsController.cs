using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeasurementsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ScaleBackgroundService _scaleService;

    public MeasurementsController(AppDbContext db, ScaleBackgroundService scaleService)
    {
        _db = db;
        _scaleService = scaleService;
    }

    /// <summary>Riwayat Pengukuran — history table</summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int? batchId,
        [FromQuery] int? samplingPoint,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20)
    {
        var query = _db.Measurements
            .Include(m => m.Batch)
            .Where(m => m.Status == "completed");

        if (batchId.HasValue) query = query.Where(m => m.BatchId == batchId.Value);
        if (samplingPoint.HasValue) query = query.Where(m => m.SamplingPoint == samplingPoint.Value);

        var total = await query.CountAsync();
        var measurements = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(m => new MeasurementDto
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
                Unit = m.Unit,
                Status = m.Status,
                PassFail = m.PassFail,
                TareCount = m.TareCount,
                BatchNoBn = m.Batch != null ? m.Batch.NoBn : null,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        // Also return batches for filter dropdown
        var batches = await _db.Batches
            .OrderBy(b => b.NoBn)
            .Select(b => new BatchDto { Id = b.Id, NoBn = b.NoBn, Status = b.Status })
            .ToListAsync();

        return Ok(new
        {
            measurements = new PaginatedResult<MeasurementDto>
            {
                Data = measurements,
                CurrentPage = page,
                LastPage = (int)Math.Ceiling((double)total / perPage),
                PerPage = perPage,
                Total = total
            },
            batches
        });
    }

    /// <summary>Start/Continue measurement — get current state for a batch+sampling point</summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartMeasurementRequest request)
    {
        var batch = await _db.Batches.FindAsync(request.BatchId);
        if (batch == null) return NotFound(new { message = "Batch tidak ditemukan." });
        if (batch.Status != "active") return BadRequest(new { message = "Batch sudah selesai." });

        var measurements = await _db.Measurements
            .Where(m => m.BatchId == request.BatchId && m.SamplingPoint == request.SamplingPoint)
            .OrderBy(m => m.MeasurementNumber)
            .ToListAsync();

        // Find current measurement to work on based on the new workflow:
        // Rule 1: Are there any measurements currently in "tare" status? If so, continue that one.
        var current = measurements.FirstOrDefault(m => m.Status == "tare");

        if (current == null)
        {
            // Rule 2: If no one is in "tare", should we create a new one?
            // Yes, if we haven't reached 80 yet.
            if (measurements.Count < 80)
            {
                var nextNumber = measurements.Count + 1;
                current = new Measurement
                {
                    BatchId = request.BatchId,
                    SamplingPoint = request.SamplingPoint,
                    MeasurementNumber = nextNumber,
                    TareReadings = new List<double>(),
                    TareCount = 0,
                    Status = "tare",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Measurements.Add(current);
                await _db.SaveChangesAsync();
                measurements.Add(current);
            }
            else
            {
                // Rule 3: We have 80 measurements, and none are in "tare". 
                // That means all have finished Tare. Now find the first one that needs Gross ("measuring").
                current = measurements.FirstOrDefault(m => m.Status == "measuring");
                
                if (current == null)
                {
                    // If current is still null here, it means all 80 are "completed".
                    return Ok(new { current = (MeasurementDto?)null, measurements = MapMeasurements(measurements), message = "Sudah mencapai 80 pengukuran." });
                }
            }
        }

        // Calculate summary statistics for all completed tare phases
        var completedMeasurements = measurements.Where(m => m.Status == "completed" || m.Status == "measuring").ToList();
        var tareSummary = GetTareSummary(completedMeasurements);

        return Ok(new
        {
            current = MapMeasurement(current),
            measurements = MapMeasurements(measurements),
            tareSummary
        });
    }

    /// <summary>Read tare weight from scale (tube kosong)</summary>
    [HttpPost("read-tare")]
    public async Task<IActionResult> ReadTare([FromBody] ReadTareRequest request)
    {
        var measurement = await _db.Measurements.FindAsync(request.MeasurementId);
        if (measurement == null) return NotFound();

        if (measurement.TareCount >= 5)
            return BadRequest(new { message = "Tare sudah 5x lengkap" });

        double? weight = await GetScaleWeight();
        if (weight == null)
        {
            if (request.ManualWeight == null)
                return BadRequest(new { message = "Timbangan tidak terhubung, masukkan berat manual." });
            weight = request.ManualWeight.Value;
        }

        var readings = measurement.TareReadings ?? new List<double>();
        readings.Add(weight.Value);
        measurement.TareReadings = readings;
        measurement.TareCount = readings.Count;

        if (measurement.TareCount >= 5)
        {
            measurement.TareAverage = readings.Average();
            measurement.TareMin = readings.Min();
            measurement.TareMax = readings.Max();
            measurement.Status = "measuring";
        }

        measurement.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, measurement = MapMeasurement(measurement) });
    }

    /// <summary>Read gross weight from scale (tube isi) — compares with recipe</summary>
    [HttpPost("read-gross")]
    public async Task<IActionResult> ReadGross([FromBody] ReadGrossRequest request)
    {
        var measurement = await _db.Measurements
            .Include(m => m.Batch)
            .FirstOrDefaultAsync(m => m.Id == request.MeasurementId);
        if (measurement == null) return NotFound();

        if (measurement.Status != "measuring")
            return BadRequest(new { message = "Selesaikan tare 5x terlebih dahulu" });

        double? weight = await GetScaleWeight();
        if (weight == null)
        {
            if (request.ManualWeight == null)
                return BadRequest(new { message = "Timbangan tidak terhubung, masukkan berat manual." });
            weight = request.ManualWeight.Value;
        }

        measurement.GrossWeight = weight.Value;
        measurement.NetWeight = weight.Value - (measurement.TareAverage ?? 0);

        // Determine Pass/Fail based on Nett vs Recipe range
        // Nett = Gross - Tare Avg
        // MS jika recipe.BobotMinimal <= Nett <= recipe.BobotMaximal
        string? passFail = null;
        if (request.RecipeId.HasValue && measurement.NetWeight.HasValue)
        {
            var recipe = await _db.Recipes.FindAsync(request.RecipeId.Value);
            if (recipe != null)
            {
                double nett = measurement.NetWeight.Value;
                passFail = (nett >= recipe.BobotMinimal && nett <= recipe.BobotMaximal) ? "Pass" : "Fail";
            }
        }

        measurement.PassFail = passFail;
        measurement.Status = "completed";
        measurement.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Load all measurements for this batch+sampling point to return updated summary
        var allMeasurements = await _db.Measurements
            .Where(m => m.BatchId == measurement.BatchId && m.SamplingPoint == measurement.SamplingPoint)
            .OrderBy(m => m.MeasurementNumber)
            .ToListAsync();

        return Ok(new
        {
            success = true,
            measurement = MapMeasurement(measurement),
            measurements = MapMeasurements(allMeasurements),
            tareSummary = GetTareSummary(allMeasurements.Where(m => m.Status == "completed" || m.Status == "measuring").ToList())
        });
    }

    /// <summary>Reset measurement (re-do tare & gross)</summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromBody] ResetMeasurementRequest request)
    {
        var measurement = await _db.Measurements.FindAsync(request.MeasurementId);
        if (measurement == null) return NotFound();

        measurement.TareReadings = new List<double>();
        measurement.TareAverage = null;
        measurement.TareMin = null;
        measurement.TareMax = null;
        measurement.GrossWeight = null;
        measurement.NetWeight = null;
        measurement.PassFail = null;
        measurement.TareCount = 0;
        measurement.Status = "tare";
        measurement.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, measurement = MapMeasurement(measurement) });
    }

    /// <summary>Reset only gross weight (keep tare data, re-do gross)</summary>
    [HttpPost("reset-gross")]
    public async Task<IActionResult> ResetGross([FromBody] ResetMeasurementRequest request)
    {
        var measurement = await _db.Measurements.FindAsync(request.MeasurementId);
        if (measurement == null) return NotFound();

        measurement.GrossWeight = null;
        measurement.NetWeight = null;
        measurement.PassFail = null;
        measurement.Status = "measuring";
        measurement.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Return updated list for this batch+sampling point
        var allMeasurements = await _db.Measurements
            .Where(m => m.BatchId == measurement.BatchId && m.SamplingPoint == measurement.SamplingPoint)
            .OrderBy(m => m.MeasurementNumber)
            .ToListAsync();

        return Ok(new { success = true, measurement = MapMeasurement(measurement), measurements = MapMeasurements(allMeasurements) });
    }

    /// <summary>Scale status + live data</summary>
    [HttpGet("scale-weight")]
    public Task<IActionResult> GetWeight()
    {
        var data = _scaleService.GetLiveData();
        var pending = _scaleService.PendingReadingsCount();
        IActionResult result = Ok(new
        {
            connected = data.Status != "DISCONNECTED",
            weight = data.Weight,
            unit = data.Unit,
            status = data.Status,
            raw = data.Raw,
            lastUpdate = data.LastUpdate,
            pendingReadings = pending
        });
        return Task.FromResult(result);
    }

    /// <summary>Consume queued stable readings from scale — auto-save to DB</summary>
    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeRequest request)
    {
        var measurement = await _db.Measurements.FindAsync(request.MeasurementId);
        if (measurement == null) return NotFound(new { message = "Measurement tidak ditemukan." });

        var reading = _scaleService.DequeueStableReading();
        if (reading == null)
        {
            return Ok(new { consumed = false, message = "Tidak ada data dari timbangan." });
        }

        if (measurement.Status == "tare")
        {
            if (measurement.TareCount >= 5)
                return Ok(new { consumed = false, message = "Tare sudah 5x lengkap." });

            var readings = measurement.TareReadings ?? new List<double>();
            readings.Add(reading.Weight);
            measurement.TareReadings = readings;
            measurement.TareCount = readings.Count;

            if (measurement.TareCount >= 5)
            {
                measurement.TareAverage = readings.Average();
                measurement.TareMin = readings.Min();
                measurement.TareMax = readings.Max();
                measurement.Status = "measuring";
            }

            measurement.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                consumed = true,
                weight = reading.Weight,
                phase = "tare",
                measurement = MapMeasurement(measurement)
            });
        }
        else if (measurement.Status == "measuring")
        {
            // NEW WORKFLOW: Only allow gross reading if ALL 80 tubes have finished tare.
            // Check how many measurements in this batch+samplingPoint still need tare.
            var totalInBatch = await _db.Measurements
                .CountAsync(m => m.BatchId == measurement.BatchId && m.SamplingPoint == measurement.SamplingPoint);
            var stillInTare = await _db.Measurements
                .CountAsync(m => m.BatchId == measurement.BatchId && m.SamplingPoint == measurement.SamplingPoint && m.Status == "tare");

            if (totalInBatch < 80 || stillInTare > 0)
            {
                // Not all 80 tubes have completed tare yet. Do NOT consume gross.
                // Put the reading back is not possible, so just skip it.
                return Ok(new { consumed = false, message = $"Fase tare belum selesai. {totalInBatch}/80 tube dibuat, {stillInTare} masih tare.", skipGross = true });
            }

            measurement.GrossWeight = reading.Weight;
            measurement.NetWeight = reading.Weight - (measurement.TareAverage ?? 0);

            // Pass/Fail: Nett vs Recipe range
            string? passFail = null;
            if (request.RecipeId.HasValue && measurement.NetWeight.HasValue)
            {
                var recipe = await _db.Recipes.FindAsync(request.RecipeId.Value);
                if (recipe != null)
                {
                    double nett = measurement.NetWeight.Value;
                    passFail = (nett >= recipe.BobotMinimal && nett <= recipe.BobotMaximal) ? "Pass" : "Fail";
                }
            }

            measurement.PassFail = passFail;
            measurement.Status = "completed";
            measurement.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                consumed = true,
                weight = reading.Weight,
                phase = "gross",
                measurement = MapMeasurement(measurement)
            });
        }

        return Ok(new { consumed = false, message = "Measurement sudah completed." });
    }

    // ─── Helpers ──────────────────────────────────────────────
    private Task<double?> GetScaleWeight()
    {
        var data = _scaleService.GetLiveData();
        if (data.Status != "DISCONNECTED")
        {
            return Task.FromResult<double?>(data.Weight);
        }
        return Task.FromResult<double?>(null);
    }

    /// <summary>Get summary statistics across all completed tube kosong measurements</summary>
    private static object GetTareSummary(List<Measurement> measurements)
    {
        var completedWithTare = measurements
            .Where(m => m.TareAverage.HasValue)
            .ToList();

        if (completedWithTare.Count == 0)
        {
            return new
            {
                count = 0,
                overallTareMin = (double?)null,
                overallTareMax = (double?)null,
                overallTareAvg = (double?)null
            };
        }

        var allTareAvgs = completedWithTare.Select(m => m.TareAverage!.Value).ToList();
        var allTareMins = completedWithTare.Where(m => m.TareMin.HasValue).Select(m => m.TareMin!.Value).ToList();
        var allTareMaxes = completedWithTare.Where(m => m.TareMax.HasValue).Select(m => m.TareMax!.Value).ToList();

        return new
        {
            count = completedWithTare.Count,
            overallTareMin = allTareMins.Count > 0 ? allTareMins.Min() : (double?)null,
            overallTareMax = allTareMaxes.Count > 0 ? allTareMaxes.Max() : (double?)null,
            overallTareAvg = allTareAvgs.Average()
        };
    }

    private static MeasurementDto MapMeasurement(Measurement m) => new()
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
        Unit = m.Unit,
        Status = m.Status,
        PassFail = m.PassFail,
        TareCount = m.TareCount,
        CreatedAt = m.CreatedAt
    };

    private static List<MeasurementDto> MapMeasurements(List<Measurement> list) =>
        list.Select(MapMeasurement).ToList();
}
