# 📚 Tài liệu chức năng Cetee — Website quản lý công việc

Bộ tài liệu này giải thích **chi tiết từng chức năng** của ứng dụng Cetee, viết theo
hướng *dễ học – dễ trình bày – dễ bảo vệ đồ án*. Mỗi file đi theo cùng một bố cục:

1. **Mục tiêu** — chức năng làm gì, vì sao cần.
2. **Khái niệm / Framework** — kiến thức nền cần nắm để giải thích (Identity, Claims, OAuth2, JWT…).
3. **Luồng hoạt động** — sơ đồ các bước từ người dùng → server → database.
4. **Các file liên quan** — chỉ rõ code nằm ở đâu (bấm vào mở được).
5. **Giải thích code chính** — đọc và hiểu từng đoạn quan trọng.
6. **Câu hỏi thường gặp khi bảo vệ (Q&A)** — câu hỏi giảng viên hay hỏi + cách trả lời.

## Mục lục

| # | Chức năng | File |
|---|-----------|------|
| 1 | Đăng nhập, Đăng ký & Phân quyền Cookie Claims (Identity) | [01-Dang-nhap-Dang-ky-Phan-quyen-Identity.md](01-Dang-nhap-Dang-ky-Phan-quyen-Identity.md) |
| 2 | Đăng nhập bên ngoài qua Google OAuth2 | [02-Dang-nhap-Google-OAuth2.md](02-Dang-nhap-Google-OAuth2.md) |
| 3 | Quên mật khẩu bằng OTP Gmail & JWT | [03-Quen-mat-khau-OTP-JWT.md](03-Quen-mat-khau-OTP-JWT.md) |
| 4 | Phân cấp tổ chức & Cách ly dữ liệu theo đội | [04-Phan-cap-To-chuc-Cach-ly-Du-lieu.md](04-Phan-cap-To-chuc-Cach-ly-Du-lieu.md) |
| 5 | Quản lý Workspace & Phân biệt Cá nhân / Nhóm | [05-Quan-ly-Workspace.md](05-Quan-ly-Workspace.md) |
| 6 | Quản lý Project & Phân bổ thành viên (Tiến độ) | [06-Quan-ly-Project.md](06-Quan-ly-Project.md) |
| 7 | Quản lý Task & Giao việc đa phụ trách (Many-to-Many) | [07-Quan-ly-Task-Da-phu-trach.md](07-Quan-ly-Task-Da-phu-trach.md) |
| 8 | Lập lịch công việc & Lịch kéo thả (Ngày/Tuần/Tháng) | [08-Lap-lich-Lich-keo-tha.md](08-Lap-lich-Lich-keo-tha.md) |
| 9 | Comment, Notification & Activity Log (+ Realtime) | [09-Comment-Notification-ActivityLog.md](09-Comment-Notification-ActivityLog.md) |
| 🚀 | Triển khai (deploy) ứng dụng lên Internet | [10-Deploy-len-Internet.md](10-Deploy-len-Internet.md) |
| 📊 | Sơ đồ tổng hợp ERD & Luồng hoạt động (Mermaid) | [11-So-do-ERD-va-Luong.md](11-So-do-ERD-va-Luong.md) |

## Kiến trúc tổng quan (đọc trước khi vào từng chức năng)

Cetee theo mô hình **MVC 3 tầng**, mỗi tầng một trách nhiệm rõ ràng:

```
Trình duyệt (Razor View)
        │  HTTP request
        ▼
┌──────────────────┐   chỉ điều phối, KHÔNG chứa nghiệp vụ
│   Controller     │   - đọc tham số, kiểm tra ModelState
│  (Controllers/)  │   - gọi Service, trả View/JSON
└────────┬─────────┘
         │  gọi interface (IXxxService)
         ▼
┌──────────────────┐   TOÀN BỘ logic + truy vấn nằm ở đây
│    Service       │   - quy tắc nghiệp vụ, phân quyền
│  (Services/)     │   - dùng LINQ trên EF Core
└────────┬─────────┘
         │
         ▼
┌──────────────────┐   ánh xạ Entity ↔ bảng SQL
│   AppDbContext   │   (EF Core - Code First)
│    (Data/)       │
└────────┬─────────┘
         ▼
     SQL Server
```

**Ba quy tắc vàng để giải thích kiến trúc:**

1. Controller **không** gọi `DbContext` trực tiếp, **không** chứa logic — chỉ điều phối.
2. Toàn bộ nghiệp vụ + truy vấn EF Core nằm trong **Service** (gộp vai trò Repository + Service cho gọn, không over-engineering).
3. **View** chỉ nhận `ViewModel`/`Entity`, không truy cập database.

### Vì sao tách `ViewModel` khỏi `Entity`?

- `Entity` (trong `Models/`) ánh xạ đúng bảng database.
- `ViewModel` (trong `ViewModels/`) là dữ liệu *riêng cho một màn hình/form*, mang theo
  **Data Annotations** để validate input (vd `[Required]`, `[EmailAddress]`).
- Nhờ vậy không lộ cấu trúc database ra View và validate được dữ liệu nhập vào.

## Bản đồ thư mục nhanh

| Thư mục | Vai trò |
|---------|---------|
| `Controllers/` | Tầng điều khiển (mỏng). `BaseController` cung cấp `CurrentUserId`, `CurrentRole`, `CanSeeAllData` lấy từ cookie claims. |
| `Models/` | Entity Code-First + hằng số (`Roles`, `AppClaims`, `Enums`). |
| `ViewModels/` | Model cho form/trang + validate. |
| `Services/` | Nghiệp vụ + truy vấn (trái tim của ứng dụng). |
| `Data/` | `AppDbContext` (cấu hình quan hệ) + `DbSeeder` (khởi tạo vai trò + SuperAdmin). |
| `Hubs/` | `RealtimeHub` — SignalR đẩy thông báo/đồng bộ realtime. |
| `Views/` | Razor views + `_Layout`. |
| `Migrations/` | Lịch sử thay đổi schema (EF Core). |
| `Program.cs` | Cấu hình DI, Identity, Google, pipeline, auto-migrate + seed. |

> 💡 Khi bảo vệ, nên mở file này trước để trình bày kiến trúc, rồi đi vào từng chức năng theo mục lục.
