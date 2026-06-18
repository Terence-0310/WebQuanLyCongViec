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

3. Chức năng chính của hệ thống
Hệ thống được thiết kế theo hướng module hóa, bao phủ đầy đủ vòng đời quản lý công việc từ xác thực người dùng → tổ chức không gian làm việc → điều phối task → cộng tác → giám sát hiệu suất. Dưới đây là các nhóm chức năng cốt lõi:
3.1. Xác thực và phân quyền (Authentication & Authorization)
Đăng ký / Đăng nhập / Đăng xuất với cơ chế bảo mật tiêu chuẩn.
Phân vai người dùng rõ ràng:
User: thao tác trong phạm vi workspace/project được cấp quyền.
Admin: toàn quyền quản trị, có khả năng quan sát và can thiệp vào toàn bộ dữ liệu hệ thống.
3.2. Quản lý không gian làm việc & dự án
Workspace (Không gian làm việc): hỗ trợ đầy đủ CRUD (Tạo – Xem – Sửa – Xóa) và liệt kê theo danh sách, giúp tổ chức công việc theo nhóm hoặc theo lĩnh vực.
Project (Dự án):
CRUD đầy đủ.
Tự động tính thanh tiến độ (%) dựa trên tỷ lệ task đã hoàn thành, giúp quản lý nắm bắt tiến độ tổng thể theo thời gian thực.
3.3. Quản lý công việc (Task Management) – Trọng tâm
a) Ghi chú (Page)
CRUD các trang ghi chú với nội dung văn bản đơn giản, phục vụ tài liệu hóa ý tưởng, biên bản cuộc họp, tài liệu dự án.
b) Task (Công việc)
CRUD task với các thuộc tính nghiệp vụ phong phú:
Giao việc (assignee), deadline, độ ưu tiên (Low / Medium / High).
Trạng thái (Status): Todo → Doing → Done.
Hai chế độ hiển thị:
📋 List View: dạng danh sách truyền thống, dễ lọc và tìm kiếm.
🗂️ Kanban Board 3 cột: kéo thả / chuyển trạng thái nhanh qua dropdown ngay trên thẻ task.
Tự động gắn nhãn 🔴 Overdue cho các task đã quá hạn, giúp nhận diện rủi ro tức thì.
c) Comment (Bình luận)
Thảo luận trực tiếp trong từng task, hiển thị theo trình tự thời gian, hỗ trợ trao đổi ngữ cảnh ngay tại nơi công việc diễn ra.
3.4. Thông báo & Nhật ký hoạt động
Notification:
Tự động phát sinh khi người dùng được giao task mới.
Hỗ trợ đánh dấu đã đọc và hiển thị badge số lượng chưa đọc trên giao diện.
Activity Log:
Ghi lại toàn bộ sự kiện quan trọng: tạo project, tạo/sửa task, chuyển trạng thái, thêm bình luận…
Phục vụ truy vết, kiểm toán và minh bạch hóa quá trình làm việc nhóm.
3.5. Dashboard – Tổng quan điều hành
Cung cấp cái nhìn toàn cảnh dành cho quản lý và cá nhân, bao gồm:
📊 Thống kê tổng hợp: số lượng workspace / project / task.
📈 Phân bố task theo trạng thái Todo / Doing / Done.
⚠️ Cảnh báo: số task quá hạn, task sắp đến deadline.
✅ Tỷ lệ hoàn thành công việc chung.
🕘 Widget tiện ích: project gần đây, hoạt động gần đây, giúp người dùng quay lại ngữ cảnh làm việc nhanh chóng.
3.6. Phân quyền và cô lập dữ liệu (Data Isolation)
User chỉ nhìn thấy và thao tác trên workspace / project / task mà mình là thành viên → đảm bảo tính riêng tư và bảo mật thông tin.
Admin có quyền xem và quản trị toàn bộ dữ liệu trên hệ thống, phục vụ vai trò giám sát tổng thể.

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

### Kiến trúc hệ thống

Hệ thống được xây dựng theo mô hình MVC và chia thành ba tầng chính gồm Controller, Service và Data. Controller chỉ tiếp nhận yêu cầu từ người dùng và điều hướng xử lý, trong khi các nghiệp vụ và truy vấn dữ liệu được đặt trong Service. Cách tổ chức này giúp mã nguồn dễ theo dõi, giảm sự phụ thuộc giữa các thành phần và thuận tiện khi bảo trì hoặc mở rộng chức năng sau này.

### Bảo mật và phân quyền

Người dùng phải đăng nhập để sử dụng các chức năng của hệ thống. Mật khẩu không được lưu trực tiếp trong cơ sở dữ liệu mà được băm bằng thuật toán PBKDF2 kết hợp với salt ngẫu nhiên nhằm tăng tính an toàn. Ngoài ra, hệ thống áp dụng phân quyền theo vai trò, trong đó Admin có quyền quản lý toàn bộ dữ liệu còn User chỉ được truy cập các workspace và project mà mình tham gia.

### Kiểm tra dữ liệu đầu vào

Các biểu mẫu trong hệ thống đều được kiểm tra dữ liệu trước khi lưu xuống cơ sở dữ liệu. Những trường bắt buộc như tiêu đề, email hoặc nội dung bình luận được kiểm tra bằng Data Annotations. Bên cạnh đó, hệ thống còn thực hiện một số kiểm tra nghiệp vụ như không cho phép tạo công việc với hạn hoàn thành nằm trong quá khứ hoặc tạo dữ liệu không thuộc phạm vi của dự án tương ứng.

### Nhật ký hoạt động

Mọi thao tác quan trọng như tạo dự án, tạo công việc, thay đổi trạng thái công việc hoặc thêm bình luận đều được ghi nhận vào bảng ActivityLog. Chức năng này giúp theo dõi lịch sử hoạt động của người dùng và hỗ trợ việc quản lý hệ thống.

### Phạm vi thực hiện

Trong phạm vi đồ án, nhóm tập trung hoàn thiện các chức năng cốt lõi như quản lý dự án, quản lý công việc, ghi chú, thông báo, phân quyền và thống kê dữ liệu. Một số chức năng nâng cao như kéo thả Kanban, cập nhật thời gian thực, trò chuyện trực tuyến hoặc tải tệp lên hệ thống chưa được triển khai để đảm bảo hoàn thành tốt các yêu cầu chính của đề tài.

### Nội dung nên trình bày trên slide

Khi báo cáo, nhóm tập trung trình bày các nội dung chính gồm:

* Sơ đồ kiến trúc MVC của hệ thống.
* Sơ đồ cơ sở dữ liệu (ERD).
* Giao diện Dashboard thống kê.
* Giao diện quản lý Project và Task.
* Bảng Kanban theo dõi tiến độ công việc.
* Chức năng phân quyền và thông báo.
* Nhật ký hoạt động của người dùng.
