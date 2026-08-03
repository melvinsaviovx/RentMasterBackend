using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMaster.Infrastructure.Persistence.Migrations;

public partial class ProtectIdentityDocumentNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "DocumentNumber",
            table: "IdentityDocuments",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(20)",
            oldMaxLength: 20);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "DocumentNumber",
            table: "IdentityDocuments",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(2048)",
            oldMaxLength: 2048);
    }
}
