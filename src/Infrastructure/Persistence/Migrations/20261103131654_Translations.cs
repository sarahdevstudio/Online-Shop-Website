using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Translations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Culture = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    OldText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NewText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Translations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Culture = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "fa-IR"),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DefaultText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Translations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationAuditLogs_Title_AtUtc",
                table: "TranslationAuditLogs",
                columns: new[] { "Title", "Culture", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Translations_Title_Culture",
                table: "Translations",
                columns: new[] { "Title", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationAuditLogs");

            migrationBuilder.DropTable(
                name: "Translations");
        }
    }
}
