using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;

namespace Cetee.Controllers;

/// <summary>Controller cơ sở: cung cấp tiện ích lấy thông tin người dùng đang đăng nhập.</summary>
public abstract class BaseController : Controller
{
    /// <summary>Id của user đang đăng nhập.</summary>
    protected int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Tên vai trò hiện tại ("SuperAdmin" / "Admin" / "Manager" / "User").</summary>
    protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? Roles.User;

    /// <summary>Chỉ SuperAdmin được phép xem/thao tác toàn bộ dữ liệu hệ thống.
    /// Các vai trò khác bị giới hạn theo quyền thành viên và phạm vi quản lý của mình.</summary>
    protected bool CanSeeAllData => Roles.CanSeeAllData(CurrentRole);

    /// <summary>Tài khoản đang đăng nhập là loại cá nhân (không thuộc cơ cấu công ty).</summary>
    protected bool IsPersonalAccount =>
        User.FindFirstValue(AppClaims.AccountType) == AccountType.Personal.ToString();
}
