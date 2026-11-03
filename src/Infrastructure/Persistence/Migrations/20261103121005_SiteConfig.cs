using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Action = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteConfigVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: true),
                    State = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteConfigVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Path = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteMedia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAuditLogs_AtUtc",
                table: "SiteAuditLogs",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAuditLogs_Kind_AtUtc",
                table: "SiteAuditLogs",
                columns: new[] { "Kind", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_SiteConfigVersions_Kind_Draft",
                table: "SiteConfigVersions",
                column: "Kind",
                unique: true,
                filter: "[State] = 'draft'");

            migrationBuilder.CreateIndex(
                name: "UX_SiteConfigVersions_Kind_Published",
                table: "SiteConfigVersions",
                column: "Kind",
                unique: true,
                filter: "[State] = 'published'");

            migrationBuilder.CreateIndex(
                name: "UX_SiteConfigVersions_Kind_Version",
                table: "SiteConfigVersions",
                columns: new[] { "Kind", "Version" },
                unique: true,
                filter: "[Version] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteAuditLogs");

            migrationBuilder.DropTable(
                name: "SiteConfigVersions");

            migrationBuilder.DropTable(
                name: "SiteMedia");
        }
    }
}
