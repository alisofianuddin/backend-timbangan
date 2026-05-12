using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

[Table("recipes")]
public class Recipe
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("nama_produk")]
    public string NamaProduk { get; set; } = string.Empty;

    [Required]
    [Column("bobot_minimal")]
    public double BobotMinimal { get; set; }

    [Required]
    [Column("bobot_maximal")]
    public double BobotMaximal { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
