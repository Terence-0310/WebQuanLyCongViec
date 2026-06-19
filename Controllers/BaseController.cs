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
}
