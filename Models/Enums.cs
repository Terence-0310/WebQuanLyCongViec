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
