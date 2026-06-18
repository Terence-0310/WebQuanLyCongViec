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
| Xác thực | **ASP.NET Core Identity** (IdentityUser/IdentityRole khóa int) + cookie; vai trò phân cấp **SuperAdmin / Admin / Manager / User** |
| Đăng nhập ngoài | **Google OAuth2** (Microsoft.AspNetCore.Authentication.Google) |
| Quên mật khẩu | **OTP gửi qua Gmail SMTP** (MailKit) + **JWT** làm vé đặt lại mật khẩu |
| Mật khẩu | Băm PBKDF2 (SHA-256, 100k vòng, salt ngẫu nhiên) |
| Giao diện | Razor Views + CSS thuần (một file `site.css`) |

## 3. Chức năng chính

- **Authentication:** đăng ký, đăng nhập, đăng xuất; phân quyền **4 cấp theo cấu
  trúc công ty** — `SuperAdmin (3) → Admin (2) → Manager (1) → User (0)` (xem
  `Models/Roles.cs`, tập trung hằng số + cấp bậc + quyền gán tại một nơi).
- **Đăng nhập bằng Google (OAuth2):** nút "Đăng nhập với Google" ở trang Login; lần
  đầu tự tạo tài khoản (Cá nhân) hoặc map vào tài khoản cùng email đã có.
- **Quên mật khẩu (OTP qua email):** nhập email → nhận **mã OTP 6 số gửi qua Gmail**
  → xác minh → đổi mật khẩu. OTP băm PBKDF2, hạn 10 phút, tối đa 5 lần thử; bước đổi
  mật khẩu được bảo vệ bằng **JWT ngắn hạn** (ký HS256) thay vì session.
- **Phân loại tài khoản (Cá nhân ↔ Nhân viên công ty):** trường `AccountType` tách
  rõ người tự đăng ký / đăng nhập Google (*Cá nhân* — tự quản việc riêng) với nhân
  viên do cấp quản lý tạo (*Nhân viên công ty* — nằm trong phân cấp). Trang Quản lý
  người dùng chia 2 nhóm; bộ chọn "xem theo người" và ứng viên thành viên chỉ tính
  nhân viên công ty.
- **Phân cấp quản lý người dùng:** mỗi cấp chỉ quản lý cấp **ngay dưới và trong đội
  của mình** — SuperAdmin tạo/quản lý Admin, Admin quản lý Manager, Manager quản lý
  User. Người tạo tự trở thành **cấp trên trực tiếp** của người được tạo
  (`ManagerId`); form không cần chọn "người quản lý". User **tự đăng ký** là *User
  độc lập* (không trực thuộc ai) để tự quản lý công việc cá nhân.
- **Cách ly theo đội:** Admin chỉ thấy/quản lý đội của mình (các Manager và User của
  các Manager đó), **không thấy Admin khác hay đội của họ**. Chỉ **SuperAdmin** mới
  xem được tổng số Admin và cấp/bỏ quyền Admin.
- **Ràng buộc an toàn:** không tự đổi quyền/xóa chính mình; **không quản/xóa người
  ngang hoặc cao cấp hơn** (hai Admin không xóa được nhau); **SuperAdmin không thể
  bị xóa dưới mọi hình thức**; chỉ SuperAdmin xóa được Admin.
- **Dashboard chỉ cho SuperAdmin & Admin:** Manager / User / User độc lập không xem
  Dashboard (đăng nhập xong về thẳng Workspaces). SuperAdmin xem số liệu **toàn hệ
  thống**, Admin xem số liệu **theo đội của mình**.
- **Xem theo người (employee scope):** trên Dashboard / List / Kanban / Timeline,
  vai trò quản lý (Manager trở lên) có **bộ chọn người** để lọc dữ liệu theo từng
  thành viên hoặc xem "Tất cả" trong phạm vi (đội) được phép.
- **Workspace:** tạo / sửa / xóa + **danh sách** tách *"của tôi"* (sở hữu) và
  *"tôi tham gia"*, mỗi thẻ có nhãn loại (**Cá nhân / Nhóm**), vai trò của bạn,
  số project/task và xếp chồng avatar thành viên. **Trang chi tiết** quản lý thành
  viên đầy đủ: thêm/gỡ người trong phạm vi quản lý, **đổi vai trò** (Thành viên ↔
  Quản lý), hiển thị chức vụ công ty + vai trò trong workspace + chủ sở hữu.
  - *Tối ưu cho cá nhân:* tài khoản **Cá nhân** (và workspace chỉ một mình) thấy
    giao diện "không gian cá nhân" gọn nhẹ — ẩn phần quản lý đội ngũ, đổi nhãn nút
    thành "Tạo không gian". Loại tài khoản được đưa vào cookie qua claim
    `account_type` (xem `AppClaims`) để giao diện không cần truy vấn lại DB.
- **Project:** CRUD + thanh tiến độ theo % task hoàn thành + **quản lý thành viên**
  (bố trí người từ thành viên workspace vào project; gỡ khỏi project). Gỡ khỏi
  workspace tự gỡ khỏi các project bên trong.
- **Page ghi chú:** CRUD, nội dung dạng văn bản đơn giản.
- **Task:** CRUD, deadline, priority (Low/Medium/High), status (Todo/Doing/Done);
  xem dạng **List**, **Kanban 3 cột** (đổi trạng thái bằng dropdown ngay trên thẻ);
  nhãn **Overdue** cho task quá hạn.
- **Lịch (Ngày / Tuần / Tháng):** chuyển nhanh giữa 3 chế độ.
  - *Ngày* — kéo / đổi / bỏ lịch và chỉnh thời lượng task bằng AJAX (`ScheduledStart`
    + `DurationMinutes`).
  - *Tuần* — lưới 7 ngày (từ Thứ 2), task xếp theo giờ trong từng ngày.
  - *Tháng* — lưới 6 tuần để **theo dõi tiến độ / "đếm công"** cả dự án; mỗi ô ngày
    hiện số việc và số hoàn thành, kèm thanh **% hoàn thành** tổng kỳ. Bấm ngày để mở
    lịch ngày tương ứng. Cả 3 chế độ đều lọc được theo người (bộ chọn "Xem của").
- **Giao việc đa phụ trách:** một task có thể giao cho **nhiều người cùng làm**
  (quan hệ nhiều-nhiều `TaskAssignee`), giao cho ai cũng được thông báo. Vì Timeline
  là **lịch riêng của từng người**, một task chung (1 khung giờ) hiển thị trên lịch
  của *tất cả* người phụ trách — nhiều người làm cùng khung giờ là bình thường,
  không bắt "mỗi người một giờ". Chỉ giao được cho thành viên của project.
- **Comment:** bình luận trong task, hiển thị theo thời gian.
- **Notification:** tự tạo khi user được giao task; đánh dấu đã đọc; badge số chưa đọc.
- **Activity Log:** ghi nhật ký khi tạo project, tạo/sửa task, đổi trạng thái,
  bình luận, xếp lịch task.
- **Dashboard:** tổng workspace / project / task, số task Todo/Doing/Done, task quá
  hạn, tỷ lệ hoàn thành, project gần đây, task sắp đến hạn, hoạt động gần đây.
- **Phân quyền dữ liệu:** "xem toàn bộ" **chỉ thuộc SuperAdmin**. Các vai trò khác
  bị giới hạn theo quyền thành viên (workspace/project) cộng phạm vi quản lý: thấy
  thêm task được giao cho người trong đội mình (sâu tối đa 2 cấp — Admin thấy việc
  của Manager lẫn User của họ).

## 4. Cấu trúc thư mục

```
Cetee/
├── Controllers/            # Tầng điều khiển (mỏng: nhận request, trả response)
│   ├── BaseController.cs        # CurrentUserId / CurrentRole / CanSeeAllData từ cookie claims
│   ├── HomeController.cs        # Trang gốc: điều hướng theo vai trò (SA/Admin->Dashboard, còn lại->Workspaces)
│   ├── AccountController.cs     # Đăng ký / đăng nhập / đăng xuất / Google OAuth / quên mật khẩu (OTP)
│   ├── DashboardController.cs   # Thống kê (chỉ SuperAdmin/Admin; có bộ chọn người - employeeId)
│   ├── UsersController.cs       # Quản lý người dùng theo đội (SuperAdmin/Admin/Manager)
│   ├── WorkspacesController.cs
│   ├── ProjectsController.cs
│   ├── PagesController.cs
│   ├── TasksController.cs       # List + Kanban + Timeline + Details + Comment + ChangeStatus + Schedule
│   └── NotificationsController.cs
├── Models/                 # Entity (Code First)
│   ├── User : IdentityUser<int> (+ FullName, RoleId, ManagerId tự tham chiếu, AccountType), Role : IdentityRole<int>, Workspace, WorkspaceMember
│   ├── Project, ProjectMember, Page
│   ├── TaskItem (+ ScheduledStart, DurationMinutes), TaskAssignee (đa phụ trách n-n), TaskComment, Notification, ActivityLog
│   ├── PasswordResetCode.cs     # Mã OTP đặt lại mật khẩu (băm, có hạn, giới hạn số lần)
│   ├── Roles.cs                 # Hằng số vai trò + cấp bậc + quyền gán (Level/AssignableBy/Label)
│   ├── AppClaims.cs             # Hằng số claim tùy biến (account_type: Personal/Company)
│   ├── EmailSettings.cs, JwtSettings.cs # POCO cấu hình (bind từ appsettings)
│   └── Enums.cs                 # TaskPriority, TaskStatus, MemberRole, AccountType
├── ViewModels/             # Model cho form/trang (chứa Data Annotations validate)
│   ├── AccountViewModels, WorkspaceViewModels, ProjectViewModels, PageViewModels
│   ├── TaskViewModels (List/Kanban/Timeline), TaskDetailsViewModel, ProjectDetailsViewModel
│   ├── DashboardViewModel
│   └── UserManagementViewModels # Create/Edit/List user + EmployeeScopeResult (xem theo người)
├── Data/
│   ├── AppDbContext.cs          # DbContext + cấu hình quan hệ/khóa ngoại
│   └── DbSeeder.cs              # Bootstrap 4 vai trò + bảo đảm luôn có 1 SuperAdmin (chủ hệ thống)
├── Services/               # Tầng nghiệp vụ (toàn bộ logic + truy vấn nằm ở đây)
│   ├── AuthService (đăng ký/Google qua UserManager), PasswordHasher (PBKDF2, cài cả IPasswordHasher<User> của Identity)
│   ├── AppUserClaimsPrincipalFactory # Sinh claim cookie: Name=FullName, Role từ RoleId
│   ├── WorkspaceService, ProjectService (+ quản lý thành viên), PageService
│   ├── TaskService, NotificationService, ActivityLogService, DashboardService
│   ├── UserService              # Phân cấp 4 tầng: CanManage/VisibleUsers theo đội, ResolveScope (xem theo người)
│   ├── EmailService             # Gửi email qua Gmail SMTP (MailKit, cổng 465 SSL)
│   ├── JwtService               # Ký/xác thực JWT (vé đặt lại mật khẩu)
│   ├── PasswordResetService     # Luồng quên mật khẩu: tạo/gửi OTP, xác minh, đổi mật khẩu
│   └── DisplayHelpers.cs        # Đổi enum -> nhãn tiếng Việt + class CSS
├── ViewComponents/         # NotificationBadge (đếm thông báo chưa đọc)
├── Views/                  # Razor views + layout sidebar
│   ├── Account/ (Login, Register, ForgotPassword, VerifyOtp, ResetPassword)
│   ├── Tasks/ (Index, Board, Timeline, Week, Month, Details, Create, Edit, _Form, _CalendarSummary)
│   ├── Users/ (Index, Create, Edit, _UserTable)
│   ├── Workspaces/ (Index, Details, Create, Edit, _Form), Projects/ (… Details)
│   └── Shared/ (_Layout, _Avatar, _EmployeePicker, Error, Components/)
├── Migrations/             # InitialCreate, AddTaskScheduling, AddManagerHierarchy, AddPasswordReset, AddAccountType, AddIdentity, AddTaskAssignees
├── seed-data.sql           # Script SSMS nạp dữ liệu mẫu công ty (tùy chọn)
├── wwwroot/css/site.css    # Toàn bộ CSS (navy + mint + xám, không rải rác)
├── appsettings.json        # Connection string + cấu hình Email/Jwt/Google (secret để ở appsettings.Development.json)
└── Program.cs              # DI, ASP.NET Core Identity + Google, pipeline, auto-migrate + seed
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
| **Role** (1) — (n) **User** | mỗi user có 1 role (SuperAdmin / Admin / Manager / User) |
| **User** (tự tham chiếu) | `ManagerId` → User (nullable): cấp trên trực tiếp trong chuỗi SuperAdmin→Admin→Manager→User; `AccountType` = Personal/Company tách Cá nhân với Nhân viên công ty |
| **Workspace** | Owner → User; (1)—(n) Project |
| **WorkspaceMember** | nối User ↔ Workspace (n-n) + MemberRole |
| **Project** | thuộc 1 Workspace; (1)—(n) Page, Task |
| **ProjectMember** | nối User ↔ Project (n-n) + MemberRole |
| **Page** | thuộc 1 Project (ghi chú) |
| **TaskItem** | thuộc 1 Project; có `ScheduledStart` + `DurationMinutes` cho Timeline |
| **TaskAssignee** | nối Task ↔ User (n-n): đa phụ trách (nhiều người cùng 1 task) |
| **TaskComment** | thuộc 1 Task + 1 User |
| **Notification** | thuộc 1 User; có thể trỏ tới 1 Task |
| **ActivityLog** | UserId, Action, EntityType, EntityId, Description, CreatedAt |
| **PasswordResetCode** | thuộc 1 User; CodeHash (OTP đã băm), ExpiresAt, Consumed, Attempts |

> Bảng task đặt tên **`TaskItem`** để tránh trùng `System.Threading.Tasks.Task`.
> Khóa ngoại cấu hình rõ ràng trong `AppDbContext`; dùng `Restrict`/`SetNull` ở
> những chỗ cần để tránh lỗi *multiple cascade paths* của SQL Server (quan hệ
> Manager tự tham chiếu dùng `Restrict`; khi xóa Manager sẽ gỡ liên kết nhân viên).
>
> **ASP.NET Core Identity** (khóa int): `User`/`Role` kế thừa IdentityUser/IdentityRole,
> bảng vẫn giữ tên `Users`/`Roles`; Identity thêm các bảng `AspNetUserClaims`,
> `AspNetUserLogins` (đăng nhập Google), `AspNetUserRoles`, `AspNetUserTokens`,
> `AspNetRoleClaims`. Vai trò dùng FK trực tiếp `User.RoleId` (1 vai trò/người).

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

### 6.1. Cấu hình Email (OTP) / JWT / Google

Cấu trúc nằm trong `appsettings.json`; **các secret** (mật khẩu SMTP, khóa JWT,
Client Secret của Google) để trong **`appsettings.Development.json`** — file này đã
được **`.gitignore`** để không bị đẩy lên git:

```json
// appsettings.Development.json
{
  "Email":  { "Password": "<App Password 16 ký tự của Gmail>" },
  "Jwt":    { "Key": "<chuỗi bí mật dài để ký JWT>" },
  "Authentication": {
    "Google": { "ClientId": "<...>.apps.googleusercontent.com", "ClientSecret": "GOCSPX-..." }
  }
}
```

- **Gmail SMTP:** bật 2FA → tạo **App Password** tại <https://myaccount.google.com/apppasswords>.
  *(App Password chỉ để **gửi mail**, khác hoàn toàn với đăng nhập Google.)*
- **Google OAuth:** tạo OAuth Client ID (Web application) tại Google Cloud Console;
  thêm **Authorized redirect URI** = `http://localhost:5259/signin-google` (đúng cổng app chạy).
- Nếu bỏ trống `ClientId` → app vẫn chạy, chỉ là nút Google không hoạt động (báo "chưa cấu hình").

## 7. Lệnh chạy migration

Cài công cụ EF (một lần duy nhất):
```bash
dotnet tool install --global dotnet-ef
```

Tạo database từ các migration có sẵn (`InitialCreate`, `AddTaskScheduling`,
`AddManagerHierarchy`, `AddPasswordReset`, `AddAccountType`, `AddIdentity`,
`AddTaskAssignees`):
```bash
dotnet ef database update
```

> Phân quyền 4 cấp **không cần migration mới**: vai trò là dữ liệu dòng trong bảng
> `Roles`, còn quan hệ phân cấp dùng cột `ManagerId` đã có sẵn.
>
> Ứng dụng cũng **tự động** `MigrateAsync()` + seed khi khởi động (xem `Program.cs`),
> nên có thể bỏ qua bước trên và chạy thẳng `dotnet run`.

Tạo lại migration từ đầu (nếu cần):
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 8. Tài khoản & dữ liệu mẫu

Khi khởi động, `DbSeeder` chỉ **bootstrap** tối thiểu để đăng nhập lần đầu: 4 vai
trò (SuperAdmin / Admin / Manager / User) và bảo đảm luôn có **đúng một SuperAdmin**
(chủ hệ thống, cấp cao nhất, không thể bị xóa):

| Vai trò | Email | Mật khẩu |
|---------|-------|----------|
| SuperAdmin | admin@example.com | Admin@123 |

> Với DB cũ đã có tài khoản Admin nhưng chưa có SuperAdmin, `DbSeeder` **tự nâng**
> `admin@example.com` (hoặc tài khoản đầu tiên) lên SuperAdmin khi khởi động — không
> cần tạo lại dữ liệu.

`DbSeeder` **không** seed workspace/project/task — toàn bộ dữ liệu nghiệp vụ do
người dùng tạo qua giao diện (hoặc nạp bằng script bên dưới).

### Dữ liệu mẫu tùy chọn (`seed-data.sql`)

Để có sẵn dữ liệu minh họa một **công ty đầy đủ 4 tầng**, mở **`seed-data.sql`**
trong SSMS (trỏ tới `CeteeDb`) và Execute. Script chỉ chèn khi DB chưa có Workspace
nào (tránh trùng lặp). Tài khoản (mật khẩu `Admin@123` cho SuperAdmin + Admin, `User@123` cho Manager/User/cá nhân):

> ⚠ File lưu UTF-8 có tiếng Việt. SSMS Execute trực tiếp là đúng; nếu chạy bằng
> `sqlcmd` phải thêm cờ codepage `-f 65001`, nếu không tên sẽ bị lỗi font.

| Vai trò | Email | Loại |
|---------|-------|------|
| SuperAdmin | admin@example.com | Nhân viên công ty |
| Admin | khoa.phan@cetee.vn (Kỹ thuật), chau.ngo@cetee.vn (Kinh doanh) | Nhân viên công ty |
| Manager | quan.le@cetee.vn (Dev), dang.vu@cetee.vn (QA), trang.do@cetee.vn (MKT) | Nhân viên công ty |
| User | ducanh.tran, mai.vo, huy.bui, nga.phan, bao.ly, nhi.ho, kiet.dinh @cetee.vn | Nhân viên công ty |
| User độc lập | ha.pham@cetee.vn, lam.trinh@cetee.vn | Cá nhân |

Kèm theo: **15 người dùng** (1 SA + 2 Admin + 3 Manager + 7 nhân viên + 2 cá nhân),
**4 workspace** (3 workspace nhóm + **1 không gian cá nhân** của ha.pham), **7 project**
có thành viên, **21 task** chia Todo/Doing/Done (có task quá hạn minh họa **Overdue**
và task đã xếp lịch trên **Timeline**), trong đó **4 task đa phụ trách** (nhiều người
cùng làm — vd "Tích hợp VNPay" giao cho 3 người), page ghi chú, comment, notification,
activity log — minh họa đầy đủ phân cấp, quản lý thành viên và tách Cá nhân/Nhân viên.

## 9. Hướng dẫn chạy project

```bash
dotnet restore
dotnet build
dotnet run
```

Mở trình duyệt tới địa chỉ in ở console (ví dụ `http://localhost:5259`).
Lần chạy đầu sẽ tự tạo database `CeteeDb` cùng tài khoản SuperAdmin bootstrap
(`admin@example.com` / `Admin@123`). Muốn có dữ liệu minh họa, nạp thêm
`seed-data.sql` (mục 8) rồi đăng nhập bằng các tài khoản ở đó.

## 10. Ghi chú dành cho báo cáo đồ án

- **Điểm nhấn kiến trúc:** tách 3 tầng rõ ràng (Controller → Service → Data),
  Controller mỏng, logic tập trung ở Service — dễ trình bày và dễ chấm.
- **Bảo mật & phân quyền phân cấp:** mật khẩu băm PBKDF2 (không lưu plain text);
  `[Authorize]` cho mọi controller cần đăng nhập, `[Authorize(Roles=...)]` giới hạn
  Dashboard (SuperAdmin/Admin) và trang quản lý người dùng (SuperAdmin/Admin/Manager);
  "xem toàn bộ" chỉ thuộc SuperAdmin, các vai trò khác bị giới hạn theo thành viên +
  đội của mình. Ràng buộc an toàn: SuperAdmin bất khả xóa, hai Admin không xóa được
  nhau, không tự đổi quyền/xóa chính mình.
- **Phân cấp tổ chức theo công ty:** `SuperAdmin → Admin → Manager → User` qua
  `ManagerId` tự tham chiếu; mỗi cấp tạo/quản lý cấp ngay dưới trong đội mình, cộng
  bộ chọn "xem theo người" trên Dashboard/List/Kanban/Timeline — điểm nhấn vượt trên
  CRUD cơ bản. Logic vai trò gom tại `Models/Roles.cs` (không hardcode rải rác).
- **Bảo mật nâng cao (điểm cộng thực tế):** đăng nhập **Google OAuth2**; **quên mật
  khẩu gửi OTP qua Gmail** (SMTP/MailKit) với mã băm + hạn dùng + giới hạn lần thử;
  **JWT** ký HS256 làm vé bảo vệ bước đổi mật khẩu; secret tách khỏi mã nguồn
  (`appsettings.Development.json` đã gitignore).
- **Tách Cá nhân / Nhân viên công ty:** trường `AccountType` phân biệt rõ hai loại
  tài khoản, phục vụ chuẩn hóa theo mô hình công ty.
- **Validation:** Data Annotations trên ViewModel (title bắt buộc, comment không
  rỗng, email hợp lệ…) + kiểm tra nghiệp vụ (deadline không được ở quá khứ khi
  tạo task mới, project phải thuộc workspace, page phải thuộc project).
- **Nhật ký hoạt động (ActivityLog):** minh họa khả năng theo dõi thao tác người
  dùng — điểm cộng khi báo cáo.
- **Phạm vi có chủ đích:** không làm realtime, block editor, chat hay upload file để
  tập trung hoàn thiện CRUD, UI, database, phân quyền và lịch ngày (Timeline có kéo
  thả/xếp lịch task bằng AJAX).
- **Gợi ý slide:** sơ đồ ERD (mục 5), sơ đồ luồng MVC (mục 4), ảnh chụp Dashboard,
  Kanban, Timeline và trang chi tiết Project/Task.
