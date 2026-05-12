using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// ─── Auth ─────────────────────────────────────────────
public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

// ─── User Management ──────────────────────────────────
public class CreateUserRequest
{
    [Required][MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required][MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required] public string PasswordConfirmation { get; set; } = string.Empty;
}

// ─── Batch ────────────────────────────────────────────
public class CreateBatchRequest
{
    [Required][MaxLength(50)] public string NoBn { get; set; } = string.Empty;
}

public class BatchDto
{
    public int Id { get; set; }
    public string NoBn { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CompletedCount { get; set; }
    public UserDto? Creator { get; set; }
    public Dictionary<int, int>? Progress { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class BatchDetailDto : BatchDto
{
    public List<MeasurementDto> Measurements { get; set; } = new();
}

// ─── Measurement ──────────────────────────────────────
public class MeasurementDto
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int SamplingPoint { get; set; }
    public int MeasurementNumber { get; set; }
    public List<double>? TareReadings { get; set; }
    public double? TareAverage { get; set; }
    public double? TareMin { get; set; }
    public double? TareMax { get; set; }
    public double? GrossWeight { get; set; }
    public double? NetWeight { get; set; }
    public string Unit { get; set; } = "g";
    public string Status { get; set; } = string.Empty;
    public string? PassFail { get; set; }
    public int TareCount { get; set; }
    public string? BatchNoBn { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ReadTareRequest
{
    [Required] public int MeasurementId { get; set; }
    public double? ManualWeight { get; set; }
}

public class ReadGrossRequest
{
    [Required] public int MeasurementId { get; set; }
    public double? ManualWeight { get; set; }
    public int? RecipeId { get; set; }
}

public class ResetMeasurementRequest
{
    [Required] public int MeasurementId { get; set; }
}

public class StartMeasurementRequest
{
    [Required] public int BatchId { get; set; }
    public int SamplingPoint { get; set; } = 1;
}

public class ConsumeRequest
{
    [Required] public int MeasurementId { get; set; }
    public int? RecipeId { get; set; }
}

// ─── Dashboard ────────────────────────────────────────
public class DashboardDto
{
    public int TotalBatches { get; set; }
    public int ActiveBatches { get; set; }
    public int TotalMeasurements { get; set; }
    public int TodayMeasurements { get; set; }
    public List<MeasurementDto> RecentMeasurements { get; set; } = new();
    public List<BatchDto> RecentBatches { get; set; } = new();
}

// ─── Reports ──────────────────────────────────────────
public class ReportStatsDto
{
    public int Count { get; set; }
    public double AvgNet { get; set; }
    public double MinNet { get; set; }
    public double MaxNet { get; set; }
    public double StdDev { get; set; }
    public double AvgTare { get; set; }
    public double AvgGross { get; set; }
}

public class ReportDto
{
    public BatchDto? SelectedBatch { get; set; }
    public List<MeasurementDto> Measurements { get; set; } = new();
    public ReportStatsDto? Stats { get; set; }
    public List<BatchDto> Batches { get; set; } = new();
}

// ─── Paginated ────────────────────────────────────────
public class PaginatedResult<T>
{
    public List<T> Data { get; set; } = new();
    public int CurrentPage { get; set; }
    public int LastPage { get; set; }
    public int PerPage { get; set; }
    public int Total { get; set; }
}

// ─── Scale ────────────────────────────────────────────
public class ScaleWeightDto
{
    public bool Connected { get; set; }
    public double Weight { get; set; }
    public string Unit { get; set; } = "g";
    public string Status { get; set; } = "DISCONNECTED";
}
