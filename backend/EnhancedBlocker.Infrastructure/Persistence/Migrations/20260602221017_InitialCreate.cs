using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnhancedBlocker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category_domains",
                columns: table => new
                {
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_domains", x => x.Domain);
                });

            migrationBuilder.CreateTable(
                name: "decision_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Tier = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    TabId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    FocusSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "focus_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeclaredIntent = table.Column<string>(type: "text", nullable: false),
                    IntentEmbedding = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_focus_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    FeaturesJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "text", nullable: false),
                    Match = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_decision_logs_Ts",
                table: "decision_logs",
                column: "Ts");

            migrationBuilder.CreateIndex(
                name: "IX_events_Domain",
                table: "events",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_events_FocusSessionId",
                table: "events",
                column: "FocusSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_events_Ts",
                table: "events",
                column: "Ts");

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_EndedAt",
                table: "focus_sessions",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_StartedAt",
                table: "focus_sessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_labels_Ts",
                table: "labels",
                column: "Ts");

            migrationBuilder.CreateIndex(
                name: "IX_rules_Pattern",
                table: "rules",
                column: "Pattern");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_domains");

            migrationBuilder.DropTable(
                name: "decision_logs");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "focus_sessions");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropTable(
                name: "rules");
        }
    }
}
