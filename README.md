# Cetee — Website quản lý công việc dự án (Notion mini + Trello mini)

## 1. Giới thiệu dự án

Cetee là ứng dụng web quản lý công việc theo dự án, kết hợp ý tưởng **ghi chú
kiểu Notion** và **bảng Kanban kiểu Trello** ở mức tối giản. Người dùng tạo
workspace → project → ghi chú (page) và quản lý công việc (task) theo 3 trạng thái
Todo / Doing / Done. Giao diện sạch, tối giản, phù hợp làm đồ án sinh viên nhưng
trông như sản phẩm thực tế.

## 2. Công nghệ sử dụng

| Thành phần | Công nghệ |
|-----------|-----------|
| Nền tảng | ASP.NET Core MVC (.NET 10) |
| ORM | Entity Framework Core (Code First) |
| Cơ sở dữ liệu | SQL Server (quản lý bằng SSMS) |
| Xác thực | Cookie Authentication (custom User/Role) |
| Mật khẩu | Băm PBKDF2 (SHA-256, 100k vòng, salt ngẫu nhiên) |
| Giao diện | Razor Views + CSS thuần (một file `site.css`) |

## 3. Chức năng chính

- **Authentication:** đăng ký, đăng nhập, đăng xuất; phân quyền **User / Admin**.
- **Workspace:** tạo / sửa / xóa / danh sách.
- **Project:** CRUD + thanh tiến độ theo % task hoàn thành.
- **Page ghi chú:** CRUD, nội dung dạng văn bản đơn giản.
- **Task:** CRUD, giao việc, deadline, priority (Low/Medium/High), status
  (Todo/Doing/Done); xem dạng **List** và **Kanban 3 cột** (đổi trạng thái bằng
  dropdown ngay trên thẻ), nhãn **Overdue** cho task quá hạn.
- **Comment:** bình luận trong task, hiển thị theo thời gian.
- **Notification:** tự tạo khi user được giao task; đánh dấu đã đọc; badge số chưa đọc.
- **Activity Log:** ghi nhật ký khi tạo project, tạo/sửa task, đổi trạng thái, bình luận.
- **Dashboard:** tổng workspace / project / task, số task Todo/Doing/Done, task quá
  hạn, tỷ lệ hoàn thành, project gần đây, task sắp đến hạn, hoạt động gần đây.
- **Phân quyền dữ liệu:** user chỉ thấy workspace/project/task mình là thành viên;
  **Admin xem toàn bộ**.

## 4. Cấu trúc thư mục

```
Cetee/
├── Controllers/            # Tầng điều khiển (mỏng: nhận request, trả response)
│   ├── BaseController.cs        # CurrentUserId / IsAdmin lấy từ cookie claims
│   ├── AccountController.cs     # Đăng ký / đăng nhập / đăng xuất
│   ├── DashboardController.cs
│   ├── WorkspacesController.cs
│   ├── ProjectsController.cs
│   ├── PagesController.cs
│   ├── TasksController.cs       # List + Kanban + Details + Comment + ChangeStatus
│   └── NotificationsController.cs
├── Models/                 # Entity (Code First)
│   ├── User, Role, Workspace, WorkspaceMember
│   ├── Project, ProjectMember, Page
│   ├── TaskItem, TaskComment, Notification, ActivityLog
│   └── Enums.cs                 # TaskPriority, TaskStatus, MemberRole
├── ViewModels/             # Model cho form/trang (chứa Data Annotations validate)
├── Data/
│   ├── AppDbContext.cs          # DbContext + cấu hình quan hệ/khóa ngoại
│   └── DbSeeder.cs              # Seed dữ liệu mẫu khi DB trống
├── Services/               # Tầng nghiệp vụ (toàn bộ logic + truy vấn nằm ở đây)
│   ├── AuthService, PasswordHasher
│   ├── WorkspaceService, ProjectService, PageService
│   ├── TaskService, NotificationService, ActivityLogService, DashboardService
│   └── DisplayHelpers.cs        # Đổi enum -> nhãn tiếng Việt + class CSS
├── ViewComponents/         # NotificationBadge (đếm thông báo chưa đọc)
├── Views/                  # Razor views + layout sidebar
├── Migrations/             # EF Core migration (InitialCreate)
├── wwwroot/css/site.css    # Toàn bộ CSS (navy + mint + xám, không rải rác)
├── appsettings.json        # Connection string
└── Program.cs              # DI, cookie auth, pipeline, auto-migrate + seed
```

### Kiến trúc

Mô hình **MVC 3 tầng**:

```
Request → Controller → Service → AppDbContext (EF Core) → SQL Server
                ↑ (chỉ điều phối)   ↑ (toàn bộ nghiệp vụ + truy vấn)
```

- Controller **không** chứa business logic, **không** gọi DbContext trực tiếp.
- View **không** truy cập DbContext; chỉ nhận ViewModel/Entity từ Controller.
- Mọi truy vấn EF Core và nghiệp vụ nằm trong **Services** (đóng vai trò
  Repository + Service gộp lại cho gọn — không over-engineering).
- ViewModel tách khỏi Entity để validate input và định hình dữ liệu cho View.

## 5. Database schema (tóm tắt)

| Bảng | Quan hệ chính |
|------|---------------|
| **Role** (1) — (n) **User** | mỗi user có 1 role (Admin/User) |
| **Workspace** | Owner → User; (1)—(n) Project |
| **WorkspaceMember** | nối User ↔ Workspace (n-n) + MemberRole |
| **Project** | thuộc 1 Workspace; (1)—(n) Page, Task |
| **ProjectMember** | nối User ↔ Project (n-n) + MemberRole |
| **Page** | thuộc 1 Project (ghi chú) |
| **TaskItem** | thuộc 1 Project; Assignee → User (nullable) |
| **TaskComment** | thuộc 1 Task + 1 User |
| **Notification** | thuộc 1 User; có thể trỏ tới 1 Task |
| **ActivityLog** | UserId, Action, EntityType, EntityId, Description, CreatedAt |

> Bảng task đặt tên **`TaskItem`** để tránh trùng `System.Threading.Tasks.Task`.
> Khóa ngoại cấu hình rõ ràng trong `AppDbContext`; dùng `Restrict`/`SetNull` ở
> những chỗ cần để tránh lỗi *multiple cascade paths* của SQL Server.

## 6. Cấu hình SQL Server

Mở `appsettings.json`, sửa chuỗi `DefaultConnection` cho đúng máy bạn:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=CeteeDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```

Ví dụ `Server=`:
- SQL Server Express:        `Server=.\SQLEXPRESS;...`
- SQL Server mặc định:       `Server=.;...` hoặc `Server=localhost;...`
- Tài khoản SQL:             `Server=localhost;User Id=sa;Password=MatKhau;...`

## 7. Lệnh chạy migration

Cài công cụ EF (một lần duy nhất):
```bash
dotnet tool install --global dotnet-ef
```

Tạo database từ migration có sẵn:
```bash
dotnet ef database update
```

> Ứng dụng cũng **tự động** `MigrateAsync()` + seed khi khởi động (xem `Program.cs`),
> nên có thể bỏ qua bước trên và chạy thẳng `dotnet run`.

Tạo lại migration từ đầu (nếu cần):
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 8. Tài khoản demo

| Vai trò | Email | Mật khẩu |
|---------|-------|----------|
| Admin | admin@example.com | Admin@123 |
| User | user1@example.com | User@123 |
| User | user2@example.com | User@123 |

Dữ liệu mẫu: 2 workspace (chính: **Website Graduation Project**), 4 project
(UI Design, Backend API, Final Report, Workshop C#), 10 task chia Todo/Doing/Done
(có 1 task quá hạn để minh họa Overdue), 3 page ghi chú (Project Overview,
Technical Documentation, Meeting Notes), comment, notification và activity log.

## 9. Hướng dẫn chạy project

```bash
dotnet restore
dotnet build
dotnet run
```

Mở trình duyệt tới địa chỉ in ở console (ví dụ `http://localhost:5259`).
Lần chạy đầu sẽ tự tạo database `CeteeDb` và nạp dữ liệu mẫu, sau đó đăng nhập
bằng tài khoản demo ở mục 8.

## 10. Ghi chú dành cho báo cáo đồ án

- **Điểm nhấn kiến trúc:** tách 3 tầng rõ ràng (Controller → Service → Data),
  Controller mỏng, logic tập trung ở Service — dễ trình bày và dễ chấm.
- **Bảo mật:** mật khẩu băm PBKDF2 (không lưu plain text); `[Authorize]` cho mọi
  controller cần đăng nhập; lọc dữ liệu theo quyền thành viên, Admin xem toàn bộ.
- **Validation:** Data Annotations trên ViewModel (title bắt buộc, comment không
  rỗng, email hợp lệ…) + kiểm tra nghiệp vụ (deadline không được ở quá khứ khi
  tạo task mới, project phải thuộc workspace, page phải thuộc project).
- **Nhật ký hoạt động (ActivityLog):** minh họa khả năng theo dõi thao tác người
  dùng — điểm cộng khi báo cáo.
- **Phạm vi có chủ đích:** không làm realtime, drag-drop, block editor, chat hay
  upload file để tập trung hoàn thiện CRUD, UI, database và phân quyền.
- **Gợi ý slide:** sơ đồ ERD (mục 5), sơ đồ luồng MVC (mục 4), ảnh chụp Dashboard,
  Kanban và trang chi tiết Project/Task.
