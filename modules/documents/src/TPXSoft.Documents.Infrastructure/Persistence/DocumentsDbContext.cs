using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence;

public sealed class DocumentsDbContext : DbContext
{
    public DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : base(options)
    {
    }

    public DbSet<Folder> Folders => Set<Folder>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentContent> DocumentContents => Set<DocumentContent>();

    public DbSet<DocumentShare> DocumentShares => Set<DocumentShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsDbContext).Assembly);
    }
}
