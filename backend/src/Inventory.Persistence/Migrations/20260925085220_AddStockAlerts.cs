using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    QuantityAtAlert = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MinThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Updater = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_CreatedDate",
                table: "StockAlerts",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_IsResolved_CreatedDate",
                table: "StockAlerts",
                columns: new[] { "IsResolved", "CreatedDate" })
                .Annotation("SqlServer:Include", new[] { "WarehouseId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_ProductId_WarehouseId",
                table: "StockAlerts",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true,
                filter: "[IsResolved] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_WarehouseId",
                table: "StockAlerts",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAlerts");
        }
    }
}
