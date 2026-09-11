using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class WorkItemListingIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_work_items_WorkspaceId_CreatedAt_Id",
            table: "work_items",
            columns: new[] { "WorkspaceId", "CreatedAt", "Id" },
            descending: new[] { false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_work_items_WorkspaceId_ProjectId_CreatedAt_Id",
            table: "work_items",
            columns: new[] { "WorkspaceId", "ProjectId", "CreatedAt", "Id" },
            descending: new[] { false, false, true, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_work_items_WorkspaceId_CreatedAt_Id",
            table: "work_items");

        migrationBuilder.DropIndex(
            name: "IX_work_items_WorkspaceId_ProjectId_CreatedAt_Id",
            table: "work_items");
    }
}
