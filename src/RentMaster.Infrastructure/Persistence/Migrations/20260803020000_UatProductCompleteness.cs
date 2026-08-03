using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMaster.Infrastructure.Persistence.Migrations;

public partial class UatProductCompleteness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DocumentNumber",
            table: "IdentityDocuments",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("UPDATE IdentityDocuments SET DocumentNumber = NumberLast4 WHERE DocumentNumber = ''");

        // Apply index changes in a new migration so databases that already ran the
        // earlier UAT migration are upgraded correctly.
        migrationBuilder.DropIndex(
            name: "IX_IdentityDocuments_NumberHash",
            table: "IdentityDocuments");

        migrationBuilder.CreateIndex(
            name: "IX_IdentityDocuments_DocumentType_NumberHash",
            table: "IdentityDocuments",
            columns: new[] { "DocumentType", "NumberHash" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [Status] IN (1, 2)");

        migrationBuilder.DropIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies");

        migrationBuilder.CreateIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies",
            column: "PropertyId",
            unique: true,
            filter: "[IsDeleted] = 0 AND [Status] IN (1, 2, 3, 6, 7)");

        migrationBuilder.CreateTable(
            name: "PropertyPhotos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StorageObjectName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PropertyPhotos", x => x.Id);
                table.ForeignKey(
                    name: "FK_PropertyPhotos_Properties_PropertyId",
                    column: x => x.PropertyId,
                    principalTable: "Properties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SupportTickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                AdminReply = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ResolvedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportTickets", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportTickets_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PropertyPhotos_PropertyId_SortOrder",
            table: "PropertyPhotos",
            columns: new[] { "PropertyId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_Status_CreatedAtUtc",
            table: "SupportTickets",
            columns: new[] { "Status", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_UserId_CreatedAtUtc",
            table: "SupportTickets",
            columns: new[] { "UserId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_IdentityDocuments_DocumentType_NumberHash",
            table: "IdentityDocuments");

        migrationBuilder.CreateIndex(
            name: "IX_IdentityDocuments_NumberHash",
            table: "IdentityDocuments",
            column: "NumberHash");

        migrationBuilder.DropIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies");

        migrationBuilder.CreateIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies",
            column: "PropertyId",
            unique: true,
            filter: "[IsDeleted] = 0 AND [Status] IN (1, 2, 3, 6)");

        migrationBuilder.DropTable(name: "PropertyPhotos");
        migrationBuilder.DropTable(name: "SupportTickets");
        migrationBuilder.DropColumn(name: "DocumentNumber", table: "IdentityDocuments");
    }
}
