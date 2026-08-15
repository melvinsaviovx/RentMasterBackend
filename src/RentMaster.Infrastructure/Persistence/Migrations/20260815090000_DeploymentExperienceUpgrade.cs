using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMaster.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260815090000_DeploymentExperienceUpgrade")]
public sealed class DeploymentExperienceUpgrade : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastSeenAtUtc",
            table: "AspNetUsers",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeliveredAtUtc",
            table: "ChatMessages",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MaintenanceRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                AssignedToUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                Priority = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                MaintenanceNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MaintenanceRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_MaintenanceRequests_AspNetUsers_AssignedToUserId",
                    column: x => x.AssignedToUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MaintenanceRequests_AspNetUsers_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MaintenanceRequests_Properties_PropertyId",
                    column: x => x.PropertyId,
                    principalTable: "Properties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MaintenanceRequests_Tenancies_TenancyId",
                    column: x => x.TenancyId,
                    principalTable: "Tenancies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_ConversationId_DeliveredAtUtc",
            table: "ChatMessages",
            columns: new[] { "ConversationId", "DeliveredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceRequests_AssignedToUserId_Status_CreatedAtUtc",
            table: "MaintenanceRequests",
            columns: new[] { "AssignedToUserId", "Status", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceRequests_CreatedByUserId",
            table: "MaintenanceRequests",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceRequests_PropertyId",
            table: "MaintenanceRequests",
            column: "PropertyId");

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceRequests_TenancyId_Status_CreatedAtUtc",
            table: "MaintenanceRequests",
            columns: new[] { "TenancyId", "Status", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MaintenanceRequests");

        migrationBuilder.DropIndex(
            name: "IX_ChatMessages_ConversationId_DeliveredAtUtc",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "DeliveredAtUtc",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "LastSeenAtUtc",
            table: "AspNetUsers");
    }
}
