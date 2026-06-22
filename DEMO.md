# 🎬 Kịch bản Demo — Cetee (Website quản lý công việc)

> Tài liệu này là **kịch bản trình bày** cho buổi bảo vệ: đi từ **demo chức năng trên hệ thống** → **kiểm thử API bằng Postman** → **xem mã nguồn trên GitHub**.
> Phần 🗣️ là **văn nói** đã soạn sẵn — có thể đọc gần như nguyên văn cho trôi chảy, chuyên nghiệp.

---

## 0. Thông tin nhanh (mở sẵn trước khi demo)

| Mục | Giá trị |
|---|---|
| **Web chính thức** | https://cetee.cloud |
| **Mã nguồn GitHub** | https://github.com/Terence-0310/WebQuanLyCongViec |
| **Công nghệ** | ASP.NET Core MVC (.NET 10), EF Core, SQL Server, ASP.NET Identity, MailKit, JWT, Google OAuth |
| **Hạ tầng** | Ubuntu (Vultr) · Docker (SQL Server) · Nginx · Cloudflare (HTTPS) |
| **File Postman** | `Cetee.postman_collection.json` (import vào Postman) |

### Tài khoản demo (mật khẩu sẵn)
| Vai trò | Email | Mật khẩu |
|---|---|---|
| **SuperAdmin** | `admin@example.com` | `Admin@123` |
| Admin | `khoa.phan@cetee.vn` | `Admin@123` |
| Manager | `quan.le@cetee.vn` | `User@123` |
| User | `ducanh.tran@cetee.vn` | `User@123` |
| Cá nhân | `ha.pham@cetee.vn` | `User@123` |

> 💡 Mẹo: mở sẵn 2 trình duyệt (1 thường + 1 ẩn danh) để đăng nhập **2 vai trò song song** (vd Manager và User) minh họa phân quyền.

---

## PHẦN 1 — Demo chức năng trên hệ thống (≈ 6–8 phút)

### 1.1 Mở đầu & Trang chủ
**Thao tác:** Mở `https://cetee.cloud` (chưa đăng nhập).

🗣️ *"Em xin trình bày đồ án **Cetee — hệ thống quản lý công việc nhóm**. Đây là sản phẩm đã được triển khai thực tế tại tên miền **cetee.cloud**, chạy trên máy chủ riêng với HTTPS qua Cloudflare. Đầu tiên là trang giới thiệu giúp người dùng mới hiểu sản phẩm trước khi đăng nhập."*

- Cuộn nhanh landing page (tính năng, cách hoạt động, lý do chọn Cetee).
- Nhấn **logo Cetee** để minh họa: ở bất kỳ trang nào, bấm logo đều quay về trang chủ.

### 1.2 Đăng nhập / Dùng thử
**Thao tác:** Bấm **Dùng thử miễn phí** (hoặc **Đăng nhập** bằng `admin@example.com`).

🗣️ *"Hệ thống có 3 cách vào: đăng ký, đăng nhập, hoặc **dùng thử một chạm** — tạo sẵn tài khoản kèm dữ liệu mẫu để trải nghiệm ngay. Em đăng nhập bằng tài khoản quản trị để demo đầy đủ."*

> Lưu ý sau đăng nhập: vào thẳng khu làm việc; bấm logo sẽ về **trang chủ**, từ đó có nút **"Vào ứng dụng"**.

### 1.3 Workspace → Project → Task (luồng nghiệp vụ chính)
**Thao tác:** Workspaces → mở 1 workspace → mở 1 project → xem bảng **Kanban**.

🗣️ *"Mô hình tổ chức gồm 3 cấp rõ ràng: **Không gian làm việc (Workspace)** chứa nhiều **Dự án (Project)**, mỗi dự án chứa các **Công việc (Task)**. Công việc được quản lý theo bảng **Kanban** ba cột: Cần làm – Đang làm – Hoàn thành; có thể kéo–thả để đổi trạng thái."*

- Tạo 1 task mới (Tiêu đề, Độ ưu tiên, Hạn, Lịch).
- Kéo task sang cột khác → minh họa cập nhật trạng thái.

### 1.4 Một việc — nhiều người phụ trách (đa phụ trách)
🗣️ *"Một điểm sát thực tế: một công việc có thể giao cho **nhiều người cùng phụ trách**, đúng với cách các đội nhóm thật phối hợp, thay vì mỗi việc chỉ một người."*

- Mở 1 task có nhiều người, chỉ vào danh sách người phụ trách.

### 1.5 Lịch: Ngày → Tuần → Tháng
**Thao tác:** Vào **Lịch**, lần lượt chuyển **Ngày / Tuần / Tháng**.

🗣️ *"Tiến độ được nhìn theo thời gian với 3 chế độ: **lịch ngày** dạng timeline theo giờ, **lịch tuần**, và **lịch tháng** tổng quan. Phía trên có thống kê số việc đã lên lịch, đã hoàn thành và phần trăm tiến độ — phục vụ việc đếm công và theo dõi dự án."*

- Ở lịch tháng: chỉ vào ô **hôm nay** (viền xanh), badge **đã hoàn thành/tổng**, và link **"+N việc khác"**.

### 1.6 Phân quyền theo vai trò
🗣️ *"Hệ thống phân cấp 4 vai trò: **SuperAdmin → Admin → Manager → User**. Mỗi vai trò thấy và thao tác đúng phạm vi của mình — ví dụ chỉ Admin/Manager mới quản lý người dùng, còn nhân viên chỉ thấy công việc của mình."*

- (Tuỳ chọn) Đăng nhập tài khoản **User** ở cửa sổ ẩn danh để so sánh: menu và dữ liệu bị giới hạn.

### 1.7 Quản lý người dùng
**Thao tác:** Menu **Người dùng** (vai trò Admin/Manager).

🗣️ *"Quản trị viên có thể tạo, sửa, đổi vai trò và xoá người dùng. Mọi thao tác xoá đều có **hộp xác nhận** để tránh sai sót, và có **thông báo dạng toast** phản hồi rõ ràng."*

- Đổi vai trò 1 người → chỉ vào **toast** góc phải; thử **Xóa** → chỉ vào **hộp xác nhận**.

### 1.8 Thông báo
🗣️ *"Khi được giao việc, người dùng nhận **thông báo** trong hệ thống; chuông trên thanh trên cùng hiển thị số chưa đọc."*

### 1.9 Quên mật khẩu — OTP qua email thật
**Thao tác:** Đăng xuất → **Quên mật khẩu** → nhập email → nhận **mã OTP gửi về Gmail thật**.

🗣️ *"Chức năng quên mật khẩu gửi **mã OTP qua email thật** (SMTP Gmail), xác minh OTP rồi cho đặt lại mật khẩu — không phải mô phỏng."*

### 1.10 Đăng nhập Google (OAuth2)
🗣️ *"Ngoài tài khoản nội bộ, hệ thống hỗ trợ **đăng nhập bằng Google** chuẩn OAuth2, đã cấu hình redirect HTTPS cho tên miền cetee.cloud."*

### 1.11 Trải nghiệm trên điện thoại
🗣️ *"Giao diện **responsive chuẩn mobile**: trên điện thoại, menu thu thành dạng trượt với nút ☰, thao tác chạm mượt."*

- (Nếu có) Mở trên điện thoại hoặc F12 → chế độ thiết bị di động.

---

## PHẦN 2 — Kiểm thử API bằng Postman (≈ 3–4 phút)

🗣️ *"Tiếp theo, em kiểm thử phía API. Toàn bộ kịch bản đã được đóng gói trong file **`Cetee.postman_collection.json`**, import vào Postman là chạy được ngay."*

### 2.1 Chuẩn bị
1. Mở Postman → **Import** → chọn `Cetee.postman_collection.json`.
2. Mở **Variables** của collection, đặt `baseUrl` = `https://cetee.cloud` (hoặc `https://localhost:xxxx` khi chạy nội bộ).

🗣️ *"Collection dùng **cookie session** và tự động lấy **antiforgery token** trước mỗi request ghi dữ liệu, mô phỏng đúng cách trình duyệt làm việc với hệ thống."*

### 2.2 Luồng chạy (theo thứ tự — logic rõ ràng)
| Bước | Thư mục / Request | Ý nghĩa |
|---|---|---|
| 1 | **Auth → Đăng nhập** | Đăng nhập, lưu cookie phiên |
| 2 | **Workspaces → Tạo / Danh sách** | Tạo không gian, lấy `workspaceId` |
| 3 | **Projects → Tạo / Danh sách** | Tạo dự án trong workspace |
| 4 | **Tasks → Tạo / Cập nhật / Lên lịch** | Tạo công việc, đổi trạng thái, xếp lịch |
| 5 | **Dashboard / Users** | Lấy số liệu, quản trị người dùng |
| 6 | **Tài khoản khác → Dùng thử** | Tạo tài khoản dùng thử qua API |

🗣️ *"Em chạy lần lượt từ đăng nhập, tạo workspace, tạo project, đến tạo và lên lịch công việc. Mỗi request trả về mã trạng thái và dữ liệu đúng kỳ vọng — chứng minh API hoạt động đầy đủ và nhất quán với giao diện."*

- Chạy **Đăng nhập** → 200/302.
- Chạy **Tạo Workspace** → kiểm tra bản ghi được tạo.
- Chạy **Tạo Task** → quay lại web reload để thấy task vừa tạo.

🗣️ *"Như vậy dữ liệu tạo từ Postman hiển thị ngay trên giao diện — back-end và front-end thống nhất một nguồn dữ liệu thật."*

---

## PHẦN 3 — Mã nguồn trên GitHub (≈ 1–2 phút)

🗣️ *"Cuối cùng là mã nguồn, được quản lý trên GitHub tại **github.com/Terence-0310/WebQuanLyCongViec**."*

Mở: **https://github.com/Terence-0310/WebQuanLyCongViec**

- **Cấu trúc dự án** (chỉ nhanh các thư mục):
  - `Controllers/` — điều hướng & xử lý request
  - `Services/` — nghiệp vụ (Auth, Task, Workspace, Email, JWT…)
  - `Models/` & `Data/` — thực thể & EF Core DbContext
  - `Views/` — giao diện Razor
  - `wwwroot/css/site.css` — hệ thống thiết kế giao diện
  - `Migrations/` — phiên bản cơ sở dữ liệu (Code First)
  - `seed-data.sql` — dữ liệu mẫu thực tế

🗣️ *"Dự án theo kiến trúc phân lớp rõ ràng: Controller mỏng, nghiệp vụ tách vào tầng Service, dữ liệu qua Entity Framework Core theo hướng Code First. Lịch sử commit thể hiện đóng góp của các thành viên trong nhóm."*

- Mở tab **Insights → Contributors** để cho thấy đóng góp của nhóm.
- (Tuỳ chọn) Mở `README.md` để xem hướng dẫn cài đặt & chạy.

---

## 🔚 Kết luận (script đóng)

🗣️ *"Tóm lại, Cetee là một hệ thống quản lý công việc **hoàn chỉnh và đã chạy thật**: từ giao diện web responsive, phân quyền theo vai trò, quản lý công việc theo Kanban và lịch tiến độ, cho tới API kiểm thử được bằng Postman, gửi email OTP thật và đăng nhập Google. Sản phẩm được triển khai trực tuyến tại **cetee.cloud** và mã nguồn công khai trên GitHub. Em xin cảm ơn thầy/cô đã lắng nghe và sẵn sàng trả lời câu hỏi."*

---

### ✅ Checklist trước khi demo
- [ ] Mở sẵn `https://cetee.cloud` và đã đăng nhập `admin@example.com`.
- [ ] Mở sẵn Postman đã import collection, đặt `baseUrl`.
- [ ] Mở sẵn tab GitHub repo + tab Contributors.
- [ ] Chuẩn bị 1 cửa sổ ẩn danh để demo vai trò User (phân quyền).
- [ ] (Nếu demo OTP) Mở sẵn hộp thư Gmail để cho thấy email nhận được.
