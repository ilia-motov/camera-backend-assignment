using Microsoft.EntityFrameworkCore;

namespace CameraApi.Infrastructure;

public sealed class CameraDbContext(DbContextOptions<CameraDbContext> options) : DbContext(options)
{
    public DbSet<CameraEntity> Cameras => Set<CameraEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var camera = modelBuilder.Entity<CameraEntity>();
        camera.ToTable("cameras");
        camera.HasKey(entity => entity.Id);
        camera.Property(entity => entity.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        camera.Property(entity => entity.Name).HasColumnName("name").IsRequired();
        camera.Property(entity => entity.Latitude).HasColumnName("latitude");
        camera.Property(entity => entity.Longitude).HasColumnName("longitude");
        camera.Property(entity => entity.IsActive).HasColumnName("is_active").HasDefaultValue(true);
    }
}
