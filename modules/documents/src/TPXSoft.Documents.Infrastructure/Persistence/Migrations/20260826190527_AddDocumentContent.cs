using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TPXSoft.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateTable(
                name: "document_contents",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_contents", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_document_contents_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_PublicLinkToken",
                table: "documents",
                column: "PublicLinkToken",
                unique: true,
                filter: "\"PublicLinkToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_contents");

            migrationBuilder.DropIndex(
                name: "IX_documents_PublicLinkToken",
                table: "documents");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);
        }
    }
}
