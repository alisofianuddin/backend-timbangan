using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BatchesController : ControllerBase
{
    private readonly AppDbContext _db;

    public BatchesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int perPage = 15)
    {
        var query = _db.Batches
            .Include(b => b.Creator)
            .Include(b => b.Measurements)
            .OrderByDescending(b => b.CreatedAt);

        var total = await query.CountAsync();
        var batches = await query
            .Skip((page - 1) * perPage)
            .Take(perPage)
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
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            })
            .ToListAsync();

        return Ok(new PaginatedResult<BatchDto>
        {
            Data = batches,
            CurrentPage = page,
            LastPage = (int)Math.Ceiling((double)total / perPage),
            PerPage = perPage,
            Total = total
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Show(int id)
    {
        var batch = await _db.Batches
            .Include(b => b.Creator)
            .Include(b => b.Measurements.OrderBy(m => m.SamplingPoint).ThenBy(m => m.MeasurementNumber))
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null) return NotFound();

        var progress = new Dictionary<int, int>();
        for (int point = 1; point <= 3; point++)
        {
            progress[point] = batch.Measurements
                .Count(m => m.SamplingPoint == point && m.Status == "completed");
        }

        return Ok(new BatchDetailDto
        {
            Id = batch.Id,
            NoBn = batch.NoBn,
            Status = batch.Status,
            CompletedCount = batch.Measurements.Count(m => m.Status == "completed"),
            Creator = batch.Creator != null ? new UserDto
            {
                Id = batch.Creator.Id,
                Name = batch.Creator.Name,
                Email = batch.Creator.Email
            } : null,
            Progress = progress,
            CreatedAt = batch.CreatedAt,
            UpdatedAt = batch.UpdatedAt,
            Measurements = batch.Measurements.Select(m => new MeasurementDto
            {
                Id = m.Id,
                BatchId = m.BatchId,
                SamplingPoint = m.SamplingPoint,
                MeasurementNumber = m.MeasurementNumber,
                TareReadings = m.TareReadings,
                TareAverage = m.TareAverage,
                GrossWeight = m.GrossWeight,
                NetWeight = m.NetWeight,
                Unit = m.Unit,
                Status = m.Status,
                TareCount = m.TareCount,
                CreatedAt = m.CreatedAt
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Store([FromBody] CreateBatchRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var batch = new Batch
        {
            NoBn = request.NoBn,
            CreatedBy = userId,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Batches.Add(batch);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = $"Batch No. BN \"{batch.NoBn}\" berhasil dibuat.", batchId = batch.Id });
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var batch = await _db.Batches.FindAsync(id);
        if (batch == null) return NotFound();

        batch.Status = "completed";
        batch.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = $"Batch \"{batch.NoBn}\" ditandai selesai." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var batch = await _db.Batches.FindAsync(id);
        if (batch == null) return NotFound();

        _db.Batches.Remove(batch);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Batch berhasil dihapus." });
    }
}
