using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

[Table("batches")]
public class Batch
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("no_bn")]
    public string NoBn { get; set; } = string.Empty;

    [Column("created_by")]
    public int CreatedBy { get; set; }

    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    [ForeignKey("CreatedBy")]
    public User? Creator { get; set; }

    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}
