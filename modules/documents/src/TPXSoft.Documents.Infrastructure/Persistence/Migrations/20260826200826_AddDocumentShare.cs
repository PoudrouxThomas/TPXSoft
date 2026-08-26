using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TPXSoft.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_shares_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_shares_DocumentId_GrantedToUserId",
                table: "document_shares",
                columns: new[] { "DocumentId", "GrantedToUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_shares_GrantedToUserId",
                table: "document_shares",
                column: "GrantedToUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_shares");
        }
    }
}
