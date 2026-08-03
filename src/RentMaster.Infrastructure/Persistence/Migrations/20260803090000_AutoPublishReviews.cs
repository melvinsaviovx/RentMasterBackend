using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMaster.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260803090000_AutoPublishReviews")]
public sealed class AutoPublishReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE [Reviews] SET [Status] = 2, [ModeratedAtUtc] = COALESCE([ModeratedAtUtc], [CreatedAtUtc]) WHERE [Status] IN (1, 4);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Published reviews are intentionally not returned to a waiting state.
    }
}
