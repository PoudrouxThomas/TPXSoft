using Microsoft.EntityFrameworkCore;
using __NAME__.Domain;

namespace __NAME__.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Todo>(todo =>
        {
            todo.HasKey(t => t.Id);
            todo.Property(t => t.Title).HasMaxLength(200).IsRequired();
            todo.Property(t => t.CreatedAt).IsRequired();
            todo.HasIndex(t => t.IsCompleted);
        });
    }
}
