using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiscountCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Total",
                table: "Orders");

            migrationBuilder.AddColumn<long>(
                name: "DiscountAmount",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCode",
                table: "Orders",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCode",
                table: "Carts",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscountAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscountCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false),
                    MinSubtotal = table.Column<long>(type: "bigint", nullable: true),
                    UsageLimit = table.Column<int>(type: "int", nullable: true),
                    PerMobileLimit = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountCodes", x => x.Id);
                    table.CheckConstraint("CK_DiscountCodes_MinSubtotal", "[MinSubtotal] IS NULL OR [MinSubtotal] >= 0");
                    table.CheckConstraint("CK_DiscountCodes_PerMobileLimit", "[PerMobileLimit] >= 1");
                    table.CheckConstraint("CK_DiscountCodes_Type", "[Type] IN (0, 1)");
                    table.CheckConstraint("CK_DiscountCodes_UsageLimit", "[UsageLimit] IS NULL OR [UsageLimit] >= 1");
                    table.CheckConstraint("CK_DiscountCodes_Value", "[Value] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DiscountCode_Status",
                table: "Orders",
                columns: new[] { "DiscountCode", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders",
                sql: "[Subtotal] >= 0 AND [ShippingFee] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [Subtotal]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Total",
                table: "Orders",
                sql: "[Total] = [Subtotal] + [ShippingFee] - [DiscountAmount]");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountAuditLogs_Code_AtUtc",
                table: "DiscountAuditLogs",
                columns: new[] { "Code", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_DiscountCodes_Code",
                table: "DiscountCodes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountAuditLogs");

            migrationBuilder.DropTable(
                name: "DiscountCodes");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DiscountCode_Status",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Total",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCode",
                table: "Carts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders",
                sql: "[Subtotal] >= 0 AND [ShippingFee] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Total",
                table: "Orders",
                sql: "[Total] = [Subtotal] + [ShippingFee]");
        }
    }
}
