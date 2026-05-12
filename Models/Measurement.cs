using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

[Table("measurements")]
public class Measurement
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("batch_id")]
    public int BatchId { get; set; }

    [Column("sampling_point")]
    public int SamplingPoint { get; set; }

    [Column("measurement_number")]
    public int MeasurementNumber { get; set; }

    /// <summary>JSON array of tare readings, e.g. [1.23, 1.24, 1.22, 1.25, 1.23]</summary>
    [Column("tare_readings")]
    public string? TareReadingsJson { get; set; }

    [NotMapped]
    public List<double>? TareReadings
    {
        get => string.IsNullOrEmpty(TareReadingsJson)
            ? new List<double>()
            : System.Text.Json.JsonSerializer.Deserialize<List<double>>(TareReadingsJson);
        set => TareReadingsJson = value == null ? null : System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Column("tare_average")]
    public double? TareAverage { get; set; }

    [Column("tare_min")]
    public double? TareMin { get; set; }

    [Column("tare_max")]
    public double? TareMax { get; set; }

    [Column("gross_weight")]
    public double? GrossWeight { get; set; }

    [Column("net_weight")]
    public double? NetWeight { get; set; }

    [MaxLength(10)]
    [Column("unit")]
    public string Unit { get; set; } = "g";

    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "tare";

    /// <summary>Pass/Fail result after gross comparison</summary>
    [MaxLength(10)]
    [Column("pass_fail")]
    public string? PassFail { get; set; }

    [Column("tare_count")]
    public int TareCount { get; set; } = 0;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    [ForeignKey("BatchId")]
    public Batch? Batch { get; set; }
}
