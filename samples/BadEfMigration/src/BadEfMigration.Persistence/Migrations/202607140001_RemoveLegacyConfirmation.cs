using Microsoft.EntityFrameworkCore.Migrations;

namespace BadEfMigration.Persistence.Migrations;

public partial class RemoveLegacyConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LegacyConfirmationCode",
            table: "Reservations");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LegacyConfirmationCode",
            table: "Reservations",
            type: "nvarchar(64)",
            nullable: true);
    }
}
