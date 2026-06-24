# Sơ đồ tổng hợp — ERD & Luồng hoạt động (Mermaid)

> Các sơ đồ dưới đây viết bằng **Mermaid**, hiển thị trực tiếp trên GitHub, VS Code
> (cài extension *Markdown Preview Mermaid Support*) hoặc dán vào <https://mermaid.live>
> để xuất ảnh PNG/SVG chèn vào slide báo cáo.

## Mục lục

1. [ERD — Sơ đồ quan hệ thực thể (toàn hệ thống)](#1-erd--sơ-đồ-quan-hệ-thực-thể)
2. [Kiến trúc MVC 3 tầng](#2-kiến-trúc-mvc-3-tầng)
3. [Cây phân cấp tổ chức (Hierarchy)](#3-cây-phân-cấp-tổ-chức-hierarchy)
4. [Luồng Đăng nhập (Identity Cookie)](#4-luồng-đăng-nhập-identity-cookie)
5. [Luồng Đăng nhập Google OAuth2](#5-luồng-đăng-nhập-google-oauth2)
6. [Luồng Quên mật khẩu (OTP + JWT)](#6-luồng-quên-mật-khẩu-otp--jwt)
7. [Luồng Tạo Task + Giao việc + Thông báo + Realtime](#7-luồng-tạo-task--giao-việc--thông-báo--realtime)
8. [Luồng Xếp lịch kéo thả (Timeline AJAX)](#8-luồng-xếp-lịch-kéo-thả-timeline-ajax)
9. [Vòng đời trạng thái Task (State)](#9-vòng-đời-trạng-thái-task-state)
10. [Sơ đồ triển khai (Deployment)](#10-sơ-đồ-triển-khai-deployment)

---

## 1. ERD — Sơ đồ quan hệ thực thể

Sinh từ cấu hình quan hệ trong [Data/AppDbContext.cs](../Data/AppDbContext.cs).

```mermaid
erDiagram
    Role ||--o{ User : "1 vai trò / nhiều user (RoleId)"
    User ||--o{ User : "ManagerId (tự tham chiếu)"

    User ||--o{ Workspace : "sở hữu (OwnerId)"
    Workspace ||--o{ WorkspaceMember : "có thành viên"
    User ||--o{ WorkspaceMember : "tham gia"

    Workspace ||--o{ Project : "chứa"
    Project ||--o{ ProjectMember : "có thành viên"
    User ||--o{ ProjectMember : "tham gia"

    Project ||--o{ Page : "ghi chú"
    Project ||--o{ TaskItem : "công việc"

    TaskItem ||--o{ TaskAssignee : "đa phụ trách (n-n)"
    User ||--o{ TaskAssignee : "được giao"

    TaskItem ||--o{ TaskComment : "bình luận"
    User ||--o{ TaskComment : "viết"

    User ||--o{ Notification : "nhận"
    TaskItem |o--o{ Notification : "có thể trỏ tới"

    User ||--o{ ActivityLog : "thao tác"
    User ||--o{ PasswordResetCode : "mã OTP"

    Role {
        int Id PK
        string Name "SuperAdmin/Admin/Manager/User"
    }
    User {
        int Id PK
        string FullName
        string Email
        string PasswordHash
        int RoleId FK
        int ManagerId FK "nullable - tự tham chiếu"
        int AccountType "Personal / Company"
    }
    Workspace {
        int Id PK
        string Name
        int OwnerId FK
    }
    WorkspaceMember {
        int WorkspaceId FK
        int UserId FK
        int Role "MemberRole: Member/Manager/Owner"
    }
    Project {
        int Id PK
        string Name
        int WorkspaceId FK
    }
    ProjectMember {
        int ProjectId FK
        int UserId FK
        int Role "MemberRole"
    }
    Page {
        int Id PK
        string Title
        int ProjectId FK
    }
    TaskItem {
        int Id PK
        string Title
        int Priority "Low/Medium/High"
        int Status "Todo/Doing/Done"
        datetime DueDate "nullable"
        datetime ScheduledStart "nullable - xếp lịch"
        int DurationMinutes
        int ProjectId FK
    }
    TaskAssignee {
        int TaskItemId PK "FK - khóa chính kép"
        int UserId PK "FK - khóa chính kép"
    }
    TaskComment {
        int Id PK
        string Content
        int TaskItemId FK
        int UserId FK
    }
    Notification {
        int Id PK
        string Message
        bool IsRead
        int UserId FK
        int TaskItemId FK "nullable - SetNull"
    }
    ActivityLog {
        int Id PK
        string Action
        string EntityType
        int EntityId
        string Description
    }
    PasswordResetCode {
        int Id PK
        string CodeHash "OTP đã băm"
        datetime ExpiresAt
        bool Consumed
        int Attempts
        int UserId FK
    }
```

> 💡 Hành vi xoá (`AppDbContext`): `Workspace→Project→Task/Page`, `Task→Assignee/Comment`,
> `User→Notification/ActivityLog/PasswordResetCode` là **Cascade**; quan hệ tới `User`
> ở các bảng nối và `Manager` tự tham chiếu dùng **Restrict** để tránh *multiple cascade
> paths* của SQL Server (xoá user/manager thì gỡ liên kết thủ công trước).

---

## 2. Kiến trúc MVC 3 tầng

```mermaid
flowchart TD
    Browser["🌐 Trình duyệt (Razor View)"]
    Ctrl["Controller<br/>(chỉ điều phối, không chứa logic)"]
    Svc["Service<br/>(toàn bộ nghiệp vụ + truy vấn LINQ)"]
    Ctx["AppDbContext (EF Core)"]
    DB[("SQL Server")]
    Hub["RealtimeHub (SignalR)"]

    Browser -- "HTTP request" --> Ctrl
    Ctrl -- "gọi IXxxService" --> Svc
    Svc -- "LINQ" --> Ctx
    Ctx -- "SQL" --> DB
    Svc -- "đẩy realtime" --> Hub
    Hub -- "WebSocket push" --> Browser
    Ctrl -- "View / JSON" --> Browser
```

---

## 3. Cây phân cấp tổ chức (Hierarchy)

Phạm vi nhìn thấy: mỗi cấp thấy **đội của mình, sâu tối đa 2 cấp** (xem
[UserService.VisibleUsers](../Services/UserService.cs)).

```mermaid
flowchart TD
    SA["👑 SuperAdmin<br/>(thấy TẤT CẢ)"]
    A1["Admin - Kỹ thuật"]
    A2["Admin - Kinh doanh"]
    M1["Manager - Dev"]
    M2["Manager - QA"]
    M3["Manager - MKT"]
    U1["User A"]
    U2["User B"]
    U3["User C"]
    P["🙍 User độc lập<br/>(Personal, ManagerId = null)"]

    SA --> A1
    SA --> A2
    A1 --> M1
    A1 --> M2
    A2 --> M3
    M1 --> U1
    M1 --> U2
    M3 --> U3

    classDef indep fill:#eef,stroke:#88a,stroke-dasharray:5 5;
    class P indep;
```

> Admin *Kỹ thuật* thấy `M1, M2` (trực tiếp) **và** `U1, U2` (qua M1) — nhưng **không**
> thấy đội của Admin *Kinh doanh*. User độc lập đứng ngoài cây, tự quản việc cá nhân.

---

## 4. Luồng Đăng nhập (Identity Cookie)

```mermaid
sequenceDiagram
    actor U as Người dùng
    participant C as AccountController
    participant SM as SignInManager (Identity)
    participant F as AppUserClaimsPrincipalFactory
    participant DB as SQL Server

    U->>C: POST /Account/Login (email, mật khẩu)
    C->>SM: PasswordSignInAsync(user, password)
    SM->>DB: lấy PasswordHash
    SM->>SM: băm lại mật khẩu nhập vào & so sánh
    alt Đúng
        SM->>F: CreateAsync(user)
        F-->>SM: Claims (Name, Role, account_type, NameIdentifier)
        SM-->>U: Set-Cookie (đã ký + mã hoá)
        C-->>U: Redirect trang chính
    else Sai
        C-->>U: "Email hoặc mật khẩu không đúng"
    end
```

---

## 5. Luồng Đăng nhập Google OAuth2

```mermaid
sequenceDiagram
    actor U as Người dùng
    participant C as AccountController
    participant G as Google
    participant A as AuthService
    participant DB as SQL Server

    U->>C: GET /Account/ExternalLogin
    C-->>U: Challenge → chuyển hướng sang Google
    U->>G: Đăng nhập + đồng ý (email, profile)
    G-->>U: Redirect về /signin-google (kèm code)
    U->>C: GET /Account/ExternalLoginCallback
    C->>A: FindOrCreateExternalUserAsync(email, name)
    A->>DB: tìm user theo email
    alt Chưa có
        A->>DB: tạo user mới (Personal, không mật khẩu)
    end
    A-->>C: user
    C-->>U: SignIn (cookie Cetee) + vào trang chính
    Note over U,G: Mật khẩu Google KHÔNG đi qua Cetee
```

---

## 6. Luồng Quên mật khẩu (OTP + JWT)

```mermaid
sequenceDiagram
    actor U as Người dùng
    participant C as AccountController
    participant R as PasswordResetService
    participant E as EmailService (Gmail)
    participant J as JwtService
    participant DB as SQL Server

    rect rgb(238,245,255)
    Note over U,DB: Bước 1 — Yêu cầu OTP
    U->>C: POST ForgotPassword (email)
    C->>R: RequestOtpAsync(email)
    R->>DB: lưu OTP đã BĂM (hạn 10', attempts=0)
    R->>E: gửi OTP 6 số qua email
    end

    rect rgb(238,255,238)
    Note over U,DB: Bước 2 — Xác minh OTP
    U->>C: POST VerifyOtp (mã)
    C->>R: VerifyOtpAsync(email, code)
    R->>DB: kiểm tra hạn / số lần / so khớp băm
    R->>J: CreateResetToken() — cấp vé JWT ngắn hạn
    J-->>C: token
    end

    rect rgb(255,245,238)
    Note over U,DB: Bước 3 — Đổi mật khẩu
    U->>C: POST ResetPassword (token, mật khẩu mới)
    C->>R: ResetPasswordAsync(token, newPassword)
    R->>J: ValidateResetToken() — chữ ký + hạn + purpose
    R->>DB: băm & lưu mật khẩu mới, xoá hết OTP
    C-->>U: "Đổi mật khẩu thành công"
    end
```

---

## 7. Luồng Tạo Task + Giao việc + Thông báo + Realtime

```mermaid
sequenceDiagram
    actor U as Người tạo
    participant C as TasksController
    participant T as TaskService
    participant N as NotificationService
    participant L as ActivityLogService
    participant H as RealtimeHub (SignalR)
    participant DB as SQL Server

    U->>C: POST /Tasks/Create
    C->>T: CreateAsync(model)
    T->>T: ValidAssigneesAsync — chỉ giữ thành viên project
    T->>DB: lưu Task + TaskAssignee (đa phụ trách)
    T->>L: LogAsync("Created", "Task")
    loop Mỗi người được giao (trừ người tạo)
        T->>N: CreateAsync("Bạn được giao task...")
        N->>DB: lưu Notification
        N->>H: push "notify" tới đúng người
    end
    T->>H: broadcast "dataChanged"
    H-->>U: badge + toast realtime (không reload)
    C-->>U: Redirect tới Chi tiết task
```

---

## 8. Luồng Xếp lịch kéo thả (Timeline AJAX)

```mermaid
sequenceDiagram
    actor U as Người dùng
    participant JS as Timeline (JavaScript)
    participant C as TasksController
    participant T as TaskService
    participant H as RealtimeHub
    participant DB as SQL Server

    U->>JS: Kéo task vào khung giờ / đổi thời lượng
    JS->>C: POST /Tasks/Schedule (id, start, duration) [AJAX]
    Note right of C: start=null → giữ giờ<br/>start="" → bỏ lịch<br/>có giá trị → đặt giờ
    C->>T: ScheduleAsync(...)
    T->>DB: cập nhật ScheduledStart / DurationMinutes
    T->>H: broadcast "dataChanged"
    C-->>JS: JSON { ok: true }
    JS-->>U: Cập nhật vị trí khối (không reload trang)
```

---

## 9. Vòng đời trạng thái Task (State)

```mermaid
stateDiagram-v2
    [*] --> Todo: Tạo task
    Todo --> Doing: Bắt đầu làm
    Doing --> Done: Hoàn thành
    Done --> Doing: Mở lại (làm tiếp)
    Doing --> Todo: Hoãn
    Todo --> Done: Xong nhanh

    note right of Todo
        Quá hạn (Overdue) =
        có DueDate + chưa Done
        + DueDate đã qua
    end note
```

---

## 10. Sơ đồ triển khai (Deployment)

Chi tiết xem [10-Deploy-len-Internet.md](10-Deploy-len-Internet.md).

```mermaid
flowchart TD
    User["🌐 Người dùng Internet"]
    CF["☁️ Cloudflare<br/>DNS + HTTPS + CDN"]

    subgraph VPS["VPS Ubuntu (Vultr)"]
        NG["nginx :80<br/>reverse proxy"]
        APP["Kestrel .NET :5000<br/>systemd: cetee.service"]
        SQL[("SQL Server 2022<br/>Docker :1433 (localhost)")]
    end

    User -- "https://cetee.cloud" --> CF
    CF -- "http :80" --> NG
    NG -- "proxy → 127.0.0.1:5000" --> APP
    APP -- "127.0.0.1:1433" --> SQL

    NG -. "Upgrade/Connection<br/>(WebSocket cho SignalR)" .-> APP
```

---

> 📌 **Gợi ý dùng cho slide:** xuất ERD (mục 1) và 1–2 sơ đồ luồng tiêu biểu (mục 4–7)
> ra ảnh tại <https://mermaid.live>. Sơ đồ kiến trúc (mục 2) và triển khai (mục 10) hợp
> làm slide mở đầu và kết thúc phần kỹ thuật.
