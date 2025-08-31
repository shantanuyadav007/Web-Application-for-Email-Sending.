using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmailSendingAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusAndAttachmentLinkToEmailLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "IsSent",
                table: "EmailLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "EmailLogs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EmailLogs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentLink",
                table: "EmailLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BccListCsv",
                table: "EmailLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CcListCsv",
                table: "EmailLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "EmailLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToListCsv",
                table: "EmailLogs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentLink",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "BccListCsv",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "CcListCsv",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "ToListCsv",
                table: "EmailLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "EmailLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EmailLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "EmailLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSent",
                table: "EmailLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
