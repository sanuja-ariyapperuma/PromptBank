using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptBank.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "TitleDescriptionEmbedding",
                table: "Prompts",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TitleDescriptionEmbedding",
                table: "Prompts");
        }
    }
}
