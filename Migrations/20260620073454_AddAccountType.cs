using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cetee.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0); // 0 = Personal mặc định

            // Backfill dữ liệu cũ: đánh dấu "Nhân viên công ty" (1) cho những ai thuộc
            // cơ cấu công ty — vai trò quản lý hoặc có cấp trên trực tiếp. Số còn lại
            // (User độc lập, tự đăng ký) giữ "Cá nhân" (0).
            migrationBuilder.Sql(@"
                UPDATE u SET u.AccountType = 1
                FROM Users u
                INNER JOIN Roles r ON r.Id = u.RoleId
                WHERE r.Name IN ('SuperAdmin','Admin','Manager') OR u.ManagerId IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Users");
        }
    }
}
