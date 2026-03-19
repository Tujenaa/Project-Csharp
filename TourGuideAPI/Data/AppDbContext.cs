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
        public DbSet<User> Users { get; set; }  // thêm cho web admin

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<POI>().ToTable("POI");
            modelBuilder.Entity<Audio>().ToTable("Audio");
            modelBuilder.Entity<History>().ToTable("History");
            modelBuilder.Entity<User>().ToTable("Users");

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