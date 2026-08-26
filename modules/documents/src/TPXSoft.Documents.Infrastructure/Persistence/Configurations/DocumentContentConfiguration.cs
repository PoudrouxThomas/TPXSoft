using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentContentConfiguration : IEntityTypeConfiguration<DocumentContent>
{
    public void Configure(EntityTypeBuilder<DocumentContent> builder)
    {
        builder.ToTable("document_contents");

        builder.HasKey(c => c.DocumentId);

        builder.Property(c => c.Bytes)
            .IsRequired();

        // 1:1 with Document, PK/FK, ON DELETE CASCADE -- unlike Document.FolderId's RESTRICT,
        // deleting a document must take its bytes with it (documentation/README.md's "Persistence").
        builder.HasOne<Document>()
            .WithOne()
            .HasForeignKey<DocumentContent>(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
