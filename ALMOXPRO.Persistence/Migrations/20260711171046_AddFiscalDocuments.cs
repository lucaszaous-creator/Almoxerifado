using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ALMOXPRO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiscal_documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessKey = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    Nsu = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    EmitterCnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    EmitterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HasFullXml = table.Column<bool>(type: "boolean", nullable: false),
                    Xml = table.Column<string>(type: "text", nullable: false),
                    ManifestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManifestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ManifestJustification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_documents_AccessKey",
                table: "fiscal_documents",
                column: "AccessKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_documents_IssuedAt",
                table: "fiscal_documents",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_documents_Status",
                table: "fiscal_documents",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscal_documents");
        }
    }
}
