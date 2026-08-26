using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.OwnerUserId)
            .IsRequired();

        builder.Property(d => d.OrgId)
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(d => d.SizeBytes)
            .IsRequired();

        builder.Property(d => d.Visibility)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.PublicLinkToken)
            .HasMaxLength(255);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        // Serves the owned branch of the listDocuments query and folderId filtering
        // (documentation/02-virtual-folders.md's "Query notes").
        builder.HasIndex(d => new { d.OwnerUserId, d.FolderId });

        // Partial index serving the org-visible branch -- only Organization-visibility rows are
        // ever matched by that predicate.
        builder.HasIndex(d => d.OrgId)
            .HasFilter("\"Visibility\" = 'Organization'");

        // Partial unique index -- only rows that actually have a public link token need to be
        // unique against each other; every non-public document has a null token and nulls never
        // collide under a partial index (documentation/README.md's "Persistence" section).
        builder.HasIndex(d => d.PublicLinkToken)
            .IsUnique()
            .HasFilter("\"PublicLinkToken\" IS NOT NULL");

        // No navigation property to Folder (Domain entities don't reference one another), but the
        // FK still needs declaring so ON DELETE RESTRICT applies: a folder with documents in it
        // cannot be deleted, matching the folder-emptiness rule in documentation 07.
        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(d => d.FolderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
