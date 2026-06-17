namespace Cetee.Models;

/// <summary>Mức độ ưu tiên của task.</summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>Trạng thái của task, ánh xạ trực tiếp với 3 cột Kanban.</summary>
public enum TaskStatus
{
    Todo = 0,
    Doing = 1,
    Done = 2
}

/// <summary>Vai trò của thành viên trong workspace/project.</summary>
public enum MemberRole
{
    Member = 0,
    Manager = 1,
    Owner = 2
}

/// <summary>
/// Loại tài khoản, tách biệt rõ người dùng cá nhân với nhân viên thuộc công ty.
/// Personal: tự đăng ký hoặc đăng nhập Google — tự quản lý công việc cá nhân, không
/// thuộc cơ cấu công ty. Company: nhân viên do cấp quản lý tạo, nằm trong phân cấp
/// SuperAdmin → Admin → Manager → User.
/// </summary>
public enum AccountType
{
    Personal = 0,
    Company = 1
}
