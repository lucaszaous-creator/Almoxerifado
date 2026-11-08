using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ALMOXPRO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedNfe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issued_nfes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessKey = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Series = table.Column<int>(type: "integer", nullable: false),
                    NatureOfOperation = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RecipientCnpjCpf = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Protocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Xml = table.Column<string>(type: "text", nullable: false),
                    IsProduction = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CanceledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelProtocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CancelJustification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_nfes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issued_nfes_AccessKey",
                table: "issued_nfes",
                column: "AccessKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issued_nfes_IssuedAt",
                table: "issued_nfes",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_issued_nfes_Series_Number",
                table: "issued_nfes",
                columns: new[] { "Series", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issued_nfes");
        }
    }
}
