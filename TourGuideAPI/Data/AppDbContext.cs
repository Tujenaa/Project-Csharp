using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Models;

namespace TourGuideAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<POI> POI { get; set; }
        public DbSet<Audio> Audio { get; set; }
        public DbSet<History> History { get; set; }
    }
}