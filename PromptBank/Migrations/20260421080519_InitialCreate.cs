using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PromptBank.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    OwnerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    RatingTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Prompts",
                columns: new[] { "Id", "Content", "CreatedAt", "IsPinned", "OwnerName", "RatingCount", "RatingTotal", "Title" },
                values: new object[,]
                {
                    { 1, "Explain the following code step by step, including what each section does and why it matters:\n\n```\n{{code}}\n```", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Dev Team", 10, 45, "Explain code" },
                    { 2, "Write xUnit unit tests for the following method. Cover happy path, edge cases, and error conditions:\n\n```\n{{method}}\n```", new DateTime(2025, 2, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "QA Team", 9, 38, "Write unit tests" },
                    { 3, "Review the following code for: correctness, security issues, performance, readability, and adherence to SOLID principles. Provide specific suggestions:\n\n```\n{{code}}\n```", new DateTime(2025, 3, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "Architecture Guild", 5, 20, "Code review checklist" },
                    { 4, "Write a SQL query to {{task}}. The table schema is:\n\n{{schema}}\n\nOptimise for readability and performance.", new DateTime(2025, 4, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "Data Team", 4, 12, "Generate SQL query" },
                    { 5, "Summarise the following pull request diff in plain English. Include what changed, why it matters, and any potential risks:\n\n{{diff}}", new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "Platform Team", 3, 8, "Summarise PR" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prompts");
        }
    }
}
