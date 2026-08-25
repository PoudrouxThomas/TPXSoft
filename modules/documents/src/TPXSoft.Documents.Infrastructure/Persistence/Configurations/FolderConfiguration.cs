using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence.Configurations;

public sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("folders");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.OwnerUserId)
            .IsRequired();

        builder.Property(f => f.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .IsRequired();

        // Serves both GET /folders (with and without a parentFolderId) and the DELETE
        // emptiness check. No unique index on (owner_user_id, parent_folder_id, name) --
        // duplicate sibling names are legal (documentation 07).
        builder.HasIndex(f => new { f.OwnerUserId, f.ParentFolderId });

        // Self-referencing FK. RESTRICT (not Cascade): deleting a folder with subfolders must
        // fail, not cascade -- delete is "empty only" (documentation 07). No navigation
        // property (Domain entities don't expose collections of one another), but the FK still
        // needs to be declared explicitly.
        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
