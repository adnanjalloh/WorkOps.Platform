using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AuditOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                MetadataJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_events", x => x.Id);
                table.ForeignKey(
                    name: "FK_audit_events_workspace_memberships_WorkspaceId_ActorUserId",
                    columns: x => new { x.WorkspaceId, x.ActorUserId },
                    principalTable: "workspace_memberships",
                    principalColumns: new[] { "WorkspaceId", "UserId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_audit_events_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
                table.UniqueConstraint("AK_outbox_messages_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                table.ForeignKey(
                    name: "FK_outbox_messages_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            columns: table => new
            {
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                Consumer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inbox_messages", x => new { x.WorkspaceId, x.MessageId, x.Consumer });
                table.ForeignKey(
                    name: "FK_inbox_messages_outbox_messages_WorkspaceId_MessageId",
                    columns: x => new { x.WorkspaceId, x.MessageId },
                    principalTable: "outbox_messages",
                    principalColumns: new[] { "WorkspaceId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_inbox_messages_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "notification_deliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Template = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_deliveries", x => x.Id);
                table.ForeignKey(
                    name: "FK_notification_deliveries_outbox_messages_WorkspaceId_SourceM~",
                    columns: x => new { x.WorkspaceId, x.SourceMessageId },
                    principalTable: "outbox_messages",
                    principalColumns: new[] { "WorkspaceId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notification_deliveries_workspace_memberships_WorkspaceId_R~",
                    columns: x => new { x.WorkspaceId, x.RecipientUserId },
                    principalTable: "workspace_memberships",
                    principalColumns: new[] { "WorkspaceId", "UserId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notification_deliveries_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_WorkspaceId_Action_OccurredAt",
            table: "audit_events",
            columns: new[] { "WorkspaceId", "Action", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_WorkspaceId_ActorUserId",
            table: "audit_events",
            columns: new[] { "WorkspaceId", "ActorUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_WorkspaceId_OccurredAt_Id",
            table: "audit_events",
            columns: new[] { "WorkspaceId", "OccurredAt", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_notification_deliveries_WorkspaceId_RecipientUserId_Created~",
            table: "notification_deliveries",
            columns: new[] { "WorkspaceId", "RecipientUserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_notification_deliveries_WorkspaceId_SourceMessageId_Recipie~",
            table: "notification_deliveries",
            columns: new[] { "WorkspaceId", "SourceMessageId", "RecipientUserId", "Channel" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_Status_NextAttemptAt_OccurredAt",
            table: "outbox_messages",
            columns: new[] { "Status", "NextAttemptAt", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_WorkspaceId_Status_OccurredAt",
            table: "outbox_messages",
            columns: new[] { "WorkspaceId", "Status", "OccurredAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_events");

        migrationBuilder.DropTable(
            name: "inbox_messages");

        migrationBuilder.DropTable(
            name: "notification_deliveries");

        migrationBuilder.DropTable(
            name: "outbox_messages");
    }
}
