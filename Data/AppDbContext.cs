using Microsoft.EntityFrameworkCore;
using Backend.Models;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<Recipe> Recipes => Set<Recipe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique constraint on measurements
        modelBuilder.Entity<Measurement>()
            .HasIndex(m => new { m.BatchId, m.SamplingPoint, m.MeasurementNumber })
            .IsUnique()
            .HasDatabaseName("unique_measurement");

        // Batch -> User relationship
        modelBuilder.Entity<Batch>()
            .HasOne(b => b.Creator)
            .WithMany()
            .HasForeignKey(b => b.CreatedBy)
            .OnDelete(DeleteBehavior.Cascade);

        // Measurement -> Batch relationship
        modelBuilder.Entity<Measurement>()
            .HasOne(m => m.Batch)
            .WithMany(b => b.Measurements)
            .HasForeignKey(m => m.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
