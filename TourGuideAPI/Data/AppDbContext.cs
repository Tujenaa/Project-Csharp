using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Models;

namespace TourGuideAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<POI> POI { get; set; }
        public DbSet<Audio> Audio { get; set; }
        public DbSet<History> History { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<POIImage> POIImages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<POI>().ToTable("POI");
            modelBuilder.Entity<Audio>().ToTable("Audio");
            modelBuilder.Entity<History>().ToTable("History");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<POIImage>().ToTable("POIImages");

            // OwnerName không có cột trong DB — ignore
            modelBuilder.Entity<POI>().Ignore(p => p.OwnerName);

            modelBuilder.Entity<Audio>()
                .HasOne(a => a.POI)
                .WithMany()
                .HasForeignKey(a => a.PoiId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<History>()
                .HasOne(h => h.POI)
                .WithMany()
                .HasForeignKey(h => h.PoiId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}