using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMaster.Infrastructure.Persistence.Migrations;

public partial class UatWorkflowImprovements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "EndRequestedByUserId",
            table: "Tenancies",
            type: "nvarchar(450)",
            maxLength: 450,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EndApprovedAtUtc",
            table: "Tenancies",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EndApprovedByUserId",
            table: "Tenancies",
            type: "nvarchar(450)",
            maxLength: 450,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EndRequestReason",
            table: "Tenancies",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "RequestedEndDate",
            table: "Tenancies",
            type: "date",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies");

        migrationBuilder.CreateIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies",
            column: "PropertyId",
            unique: true,
            filter: "[IsDeleted] = 0 AND [Status] IN (1, 2, 3, 6)");

        migrationBuilder.CreateTable(
            name: "ChatConversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                TenantUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                RentalApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TenancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastMessageAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatConversations", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChatConversations_AspNetUsers_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChatConversations_AspNetUsers_TenantUserId",
                    column: x => x.TenantUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChatConversations_Properties_PropertyId",
                    column: x => x.PropertyId,
                    principalTable: "Properties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChatConversations_RentalApplications_RentalApplicationId",
                    column: x => x.RentalApplicationId,
                    principalTable: "RentalApplications",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChatConversations_Tenancies_TenancyId",
                    column: x => x.TenancyId,
                    principalTable: "Tenancies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SenderUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                IsSystemMessage = table.Column<bool>(type: "bit", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChatMessages_AspNetUsers_SenderUserId",
                    column: x => x.SenderUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChatMessages_ChatConversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "ChatConversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_OwnerUserId_LastMessageAtUtc",
            table: "ChatConversations",
            columns: new[] { "OwnerUserId", "LastMessageAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_PropertyId_OwnerUserId_TenantUserId",
            table: "ChatConversations",
            columns: new[] { "PropertyId", "OwnerUserId", "TenantUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_RentalApplicationId",
            table: "ChatConversations",
            column: "RentalApplicationId",
            unique: true,
            filter: "[RentalApplicationId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_TenancyId",
            table: "ChatConversations",
            column: "TenancyId",
            unique: true,
            filter: "[TenancyId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_TenantUserId_LastMessageAtUtc",
            table: "ChatConversations",
            columns: new[] { "TenantUserId", "LastMessageAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_ConversationId_CreatedAtUtc",
            table: "ChatMessages",
            columns: new[] { "ConversationId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_ConversationId_ReadAtUtc",
            table: "ChatMessages",
            columns: new[] { "ConversationId", "ReadAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_SenderUserId",
            table: "ChatMessages",
            column: "SenderUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ChatMessages");
        migrationBuilder.DropTable(name: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies");

        migrationBuilder.CreateIndex(
            name: "IX_Tenancies_PropertyId",
            table: "Tenancies",
            column: "PropertyId",
            unique: true,
            filter: "[IsDeleted] = 0 AND [Status] IN (1, 2, 3)");

        migrationBuilder.DropColumn(name: "EndApprovedAtUtc", table: "Tenancies");
        migrationBuilder.DropColumn(name: "EndApprovedByUserId", table: "Tenancies");
        migrationBuilder.DropColumn(name: "EndRequestReason", table: "Tenancies");
        migrationBuilder.DropColumn(name: "RequestedEndDate", table: "Tenancies");

        migrationBuilder.AlterColumn<string>(
            name: "EndRequestedByUserId",
            table: "Tenancies",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)",
            oldMaxLength: 450,
            oldNullable: true);
    }
}
