using Microsoft.EntityFrameworkCore;
using SpamClassifierAPI.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SpamClassifierAPI.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Global soft-delete filter — IsDeleted users are excluded from all queries
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        }

        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<User>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser;
                }
            }
        }
    }
}
