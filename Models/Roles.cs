namespace Cetee.Models;

/// <summary>
/// Bộ vai trò phân cấp theo cấu trúc công ty và tiện ích so sánh cấp bậc.
/// Tập trung tại một nơi để tránh "hardcode" chuỗi vai trò rải rác trong code.
///
/// Phân cấp quản lý (cấp cao quản lý cấp ngay dưới):
///   SuperAdmin (3) → Admin (2) → Manager (1) → User (0)
///
/// Quy ước quan hệ trực thuộc lưu ở <see cref="User.ManagerId"/> (tự tham chiếu):
/// một người là cấp trên trực tiếp của người khác. User độc lập (tự đăng ký) có
/// vai trò User và không trực thuộc ai (ManagerId = null) để tự quản lý việc cá nhân.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    /// <summary>Tất cả vai trò, từ cao xuống thấp.</summary>
    public static readonly string[] All = { SuperAdmin, Admin, Manager, User };

    /// <summary>Cấp bậc của vai trò; số lớn hơn = quyền cao hơn. Vai trò lạ -> 0.</summary>
    public static int Level(string? role) => role switch
    {
        SuperAdmin => 3,
        Admin => 2,
        Manager => 1,
        _ => 0
    };

    /// <summary>Chỉ SuperAdmin được xem/thao tác toàn bộ dữ liệu hệ thống.</summary>
    public static bool CanSeeAllData(string? role) => role == SuperAdmin;

    /// <summary>Chỉ SuperAdmin và Admin được xem trang Dashboard.</summary>
    public static bool CanAccessDashboard(string? role) => role == SuperAdmin || role == Admin;

    /// <summary>Những vai trò mà người có vai trò <paramref name="viewerRole"/> được phép gán
    /// cho người khác (luôn thấp hơn cấp của chính mình — không ai tự tạo người ngang/cao hơn).</summary>
    public static IEnumerable<string> AssignableBy(string? viewerRole)
    {
        int max = Level(viewerRole);
        return All.Where(r => Level(r) < max);
    }

    /// <summary>Nhãn tiếng Việt hiển thị cho vai trò.</summary>
    public static string Label(string? role) => role switch
    {
        SuperAdmin => "Super Admin",
        Admin => "Admin (Quản trị)",
        Manager => "Manager (Trưởng nhóm)",
        User => "User (Nhân viên / Cá nhân)",
        _ => role ?? ""
    };
}
