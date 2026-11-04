using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoutePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RouteAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    OldPath = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    NewPath = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutePaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Path = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    TargetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    IsGone = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutePaths", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteAuditLogs_AtUtc",
                table: "RouteAuditLogs",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RouteAuditLogs_Target",
                table: "RouteAuditLogs",
                columns: new[] { "TargetType", "TargetId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_RoutePaths_Current",
                table: "RoutePaths",
                columns: new[] { "TargetType", "TargetId" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_RoutePaths_Path",
                table: "RoutePaths",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteAuditLogs");

            migrationBuilder.DropTable(
                name: "RoutePaths");
        }
    }
}
