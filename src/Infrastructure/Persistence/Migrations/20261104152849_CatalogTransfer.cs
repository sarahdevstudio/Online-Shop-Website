using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogTransferLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rows = table.Column<int>(type: "int", nullable: false),
                    ProductsCreated = table.Column<int>(type: "int", nullable: false),
                    ProductsUpdated = table.Column<int>(type: "int", nullable: false),
                    VariantsCreated = table.Column<int>(type: "int", nullable: false),
                    VariantsUpdated = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogTransferLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogTransferLogs_AtUtc",
                table: "CatalogTransferLogs",
                column: "AtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogTransferLogs");
        }
    }
}
