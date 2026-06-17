using Cetee.Models;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

/// <summary>Tiện ích chuyển enum sang nhãn tiếng Việt và class CSS để dùng trong View.</summary>
public static class DisplayHelpers
{
    public static string Label(this TaskStatus status) => status switch
    {
        TaskStatus.Todo => "Cần làm",
        TaskStatus.Doing => "Đang làm",
        TaskStatus.Done => "Hoàn thành",
        _ => status.ToString()
    };

    public static string CssClass(this TaskStatus status) => status switch
    {
        TaskStatus.Todo => "tag-todo",
        TaskStatus.Doing => "tag-doing",
        TaskStatus.Done => "tag-done",
        _ => "tag-todo"
    };

    public static string Label(this AccountType type) => type switch
    {
        AccountType.Company => "Nhân viên công ty",
        _ => "Cá nhân"
    };

    public static string CssClass(this AccountType type) => type switch
    {
        AccountType.Company => "tag-doing",
        _ => "tag-todo"
    };

    public static string Label(this MemberRole role) => role switch
    {
        MemberRole.Owner => "Chủ sở hữu",
        MemberRole.Manager => "Quản lý",
        _ => "Thành viên"
    };

    public static string CssClass(this MemberRole role) => role switch
    {
        MemberRole.Owner => "tag-high",
        MemberRole.Manager => "tag-doing",
        _ => "tag-todo"
    };

    public static string Label(this TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "Thấp",
        TaskPriority.Medium => "Trung bình",
        TaskPriority.High => "Cao",
        _ => priority.ToString()
    };

    public static string CssClass(this TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "tag-low",
        TaskPriority.Medium => "tag-medium",
        TaskPriority.High => "tag-high",
        _ => "tag-low"
    };

    /// <summary>Lấy 2 ký tự đầu của tên để hiển thị avatar dạng chữ cái.</summary>
    public static string Initials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "?";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();

        // Lấy chữ cái đầu của từ đầu và từ cuối (ví dụ "Nguyễn Văn An" -> "NA").
        return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>Chọn một màu avatar ổn định dựa trên tên (số 0..5 ứng với class avatar-c0..c5).</summary>
    public static int AvatarColor(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return 0;
        int sum = fullName.Sum(c => c);
        return Math.Abs(sum) % 6;
    }
}
