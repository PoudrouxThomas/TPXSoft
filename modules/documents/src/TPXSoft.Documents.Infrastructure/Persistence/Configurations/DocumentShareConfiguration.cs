using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentShareConfiguration : IEntityTypeConfiguration<DocumentShare>
{
    public void Configure(EntityTypeBuilder<DocumentShare> builder)
    {
        builder.ToTable("document_shares");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DocumentId)
            .IsRequired();

        builder.Property(s => s.GrantedToUserId)
            .IsRequired();

        builder.Property(s => s.GrantedByUserId)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // Backs the 409 ShareAlreadyExists rule -- a second grant for the same
        // (document, user) pair is rejected by this index even if two concurrent requests both
        // pass the service-level check (documentation/04-sharing-and-visibility.md's
        // shareDocumentWithUser section).
        builder.HasIndex(s => new { s.DocumentId, s.GrantedToUserId })
            .IsUnique();

        // For a future "shared with me" view (GET /documents?sharedWithMe=true), not built yet --
        // documentation/04-sharing-and-visibility.md's Open questions.
        builder.HasIndex(s => s.GrantedToUserId);

        // No navigation property to Document (Domain entities don't reference one another), but
        // the FK still needs declaring so ON DELETE CASCADE applies: deleting a document must take
        // its share grants with it.
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(s => s.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
