using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Cetee.Controllers;

/// <summary>Controller cơ sở: cung cấp tiện ích lấy thông tin người dùng đang đăng nhập.</summary>
public abstract class BaseController : Controller
{
    /// <summary>Id của user đang đăng nhập.</summary>
    protected int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Người dùng hiện tại có vai trò Admin hay không.</summary>
    protected bool IsAdmin => User.IsInRole("Admin");

    /// <summary>Người dùng hiện tại có vai trò Manager (quản lý) hay không.</summary>
    protected bool IsManager => User.IsInRole("Manager");

    /// <summary>Tên vai trò hiện tại ("Admin" / "Manager" / "User").</summary>
    protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "User";
}
