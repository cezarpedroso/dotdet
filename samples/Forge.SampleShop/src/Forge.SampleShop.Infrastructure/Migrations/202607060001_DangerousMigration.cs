using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forge.SampleShop.Infrastructure.Migrations;

public partial class DangerousMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LegacyCode",
            table: "Orders");

        migrationBuilder.Sql("DELETE FROM Orders WHERE CustomerEmail IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LegacyCode",
            table: "Orders",
            type: "nvarchar(max)",
            nullable: true);
    }
}
