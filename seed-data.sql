/* =====================================================================
   seed-data.sql — Dữ liệu mẫu thực tế cho Cetee (Website quản lý công việc)
   ---------------------------------------------------------------------
   CÁCH DÙNG (trong SSMS):
     1. Tạo schema trước: chạy `dotnet ef database update` hoặc chạy app 1 lần
        (Program.cs tự migrate khi khởi động).
     2. Mở file này trong SSMS, đảm bảo đang trỏ tới database CeteeDb, bấm Execute (F5).
   Script tự bảo đảm có vai trò Admin/User và tài khoản admin@example.com;
   chỉ chèn dữ liệu mẫu khi chưa có Workspace nào (tránh trùng lặp).

   Tài khoản đăng nhập:
     - admin@example.com  / Admin@123  (Admin)
     - quan.le@cetee.vn   / User@123
     - ha.pham@cetee.vn   / User@123
     - ducanh.tran@cetee.vn / User@123
     - mai.vo@cetee.vn    / User@123
   Mật khẩu lưu dạng băm PBKDF2 (SHA-256, 100k vòng) đúng định dạng app dùng.
   ===================================================================== */

USE CeteeDb;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Hash cố định cho mật khẩu demo (định dạng: iterations.base64(salt).base64(key))
DECLARE @adminHash NVARCHAR(MAX) = N'100000.eHw39f/S2WXvX9bCGs8eUQ==.YdmeFVzvCeARDcJbF3ZA72U2M4y/6lRq9xiRvFFzAc8=';
DECLARE @userHash  NVARCHAR(MAX) = N'100000.ffGk9b8zGWcNKTzrUiLCkQ==.9mx9yRhJ2GWYUI5BynWrZyGJr/5R5M9VWf7cv62zjvc=';

DECLARE @now   DATETIME2 = GETUTCDATE();
DECLARE @today DATETIME2 = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2);

-- 1) Bảo đảm có vai trò Admin/Manager/User
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Admin')   INSERT INTO Roles(Name) VALUES (N'Admin');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Manager') INSERT INTO Roles(Name) VALUES (N'Manager');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'User')    INSERT INTO Roles(Name) VALUES (N'User');

DECLARE @adminRole INT = (SELECT Id FROM Roles WHERE Name = 'Admin');
DECLARE @mgrRole   INT = (SELECT Id FROM Roles WHERE Name = 'Manager');
DECLARE @userRole  INT = (SELECT Id FROM Roles WHERE Name = 'User');

-- 2) Bảo đảm có tài khoản Admin bootstrap
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@example.com')
    INSERT INTO Users (FullName, Email, PasswordHash, RoleId, CreatedAt)
    VALUES (N'Quản trị viên', 'admin@example.com', @adminHash, @adminRole, @now);

DECLARE @admin INT = (SELECT TOP 1 Id FROM Users WHERE Email = 'admin@example.com');

-- 3) Chỉ chèn dữ liệu mẫu khi DB chưa có Workspace nào
IF EXISTS (SELECT 1 FROM Workspaces)
BEGIN
    PRINT N'Đã có dữ liệu workspace — bỏ qua phần seed mẫu.';
END
ELSE
BEGIN
    BEGIN TRAN;

    -- ===== Users (mật khẩu: User@123) =====
    -- Lê Minh Quân là Manager; Trần Đức Anh và Võ Thị Mai là nhân viên trực thuộc Quân.
    -- Phạm Thu Hà là người dùng độc lập (không trực thuộc ai).
    INSERT INTO Users (FullName,Email,PasswordHash,RoleId,CreatedAt) VALUES (N'Lê Minh Quân','quan.le@cetee.vn',@userHash,@mgrRole,DATEADD(day,-40,@now));
    DECLARE @quan INT=SCOPE_IDENTITY();
    INSERT INTO Users (FullName,Email,PasswordHash,RoleId,CreatedAt) VALUES (N'Phạm Thu Hà','ha.pham@cetee.vn',@userHash,@userRole,DATEADD(day,-38,@now));
    DECLARE @ha INT=SCOPE_IDENTITY();
    INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Trần Đức Anh','ducanh.tran@cetee.vn',@userHash,@userRole,@quan,DATEADD(day,-35,@now));
    DECLARE @ducanh INT=SCOPE_IDENTITY();
    INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Võ Thị Mai','mai.vo@cetee.vn',@userHash,@userRole,@quan,DATEADD(day,-33,@now));
    DECLARE @mai INT=SCOPE_IDENTITY();

    -- ===== Workspaces =====
    INSERT INTO Workspaces (Name,Description,OwnerId,CreatedAt) VALUES (N'Phòng Phát triển Sản phẩm', N'Không gian làm việc của đội kỹ thuật, quản lý các sản phẩm phần mềm đang phát triển.', @quan, DATEADD(day,-35,@now));
    DECLARE @wsDev INT=SCOPE_IDENTITY();
    INSERT INTO Workspaces (Name,Description,OwnerId,CreatedAt) VALUES (N'Marketing & Truyền thông', N'Quản lý các chiến dịch quảng bá và nội dung truyền thông của công ty.', @ha, DATEADD(day,-32,@now));
    DECLARE @wsMkt INT=SCOPE_IDENTITY();

    INSERT INTO WorkspaceMembers (WorkspaceId,UserId,Role,JoinedAt) VALUES
        (@wsDev,@quan,2,DATEADD(day,-35,@now)),(@wsDev,@ducanh,0,DATEADD(day,-34,@now)),(@wsDev,@mai,0,DATEADD(day,-33,@now)),(@wsDev,@admin,1,DATEADD(day,-30,@now)),
        (@wsMkt,@ha,2,DATEADD(day,-32,@now)),(@wsMkt,@mai,0,DATEADD(day,-31,@now)),(@wsMkt,@admin,1,DATEADD(day,-30,@now));

    -- ===== Projects =====
    INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Website Thương mại điện tử', N'Xây dựng website bán hàng trực tuyến với giỏ hàng và thanh toán.', @wsDev, DATEADD(day,-30,@now));
    DECLARE @pWeb INT=SCOPE_IDENTITY();
    INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Ứng dụng Mobile Bán hàng', N'Ứng dụng di động cho khách hàng đặt mua sản phẩm.', @wsDev, DATEADD(day,-25,@now));
    DECLARE @pMobile INT=SCOPE_IDENTITY();
    INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Hệ thống Quản lý Kho', N'Phần mềm quản lý tồn kho, nhập xuất và cảnh báo hàng hóa.', @wsDev, DATEADD(day,-20,@now));
    DECLARE @pKho INT=SCOPE_IDENTITY();
    INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Chiến dịch Ra mắt Q3', N'Kế hoạch truyền thông cho đợt ra mắt sản phẩm quý 3.', @wsMkt, DATEADD(day,-15,@now));
    DECLARE @pCampaign INT=SCOPE_IDENTITY();

    INSERT INTO ProjectMembers (ProjectId,UserId,Role,JoinedAt) VALUES
        (@pWeb,@ducanh,2,DATEADD(day,-30,@now)),(@pWeb,@mai,0,DATEADD(day,-29,@now)),(@pWeb,@quan,1,DATEADD(day,-29,@now)),
        (@pMobile,@mai,2,DATEADD(day,-25,@now)),(@pMobile,@ducanh,0,DATEADD(day,-24,@now)),
        (@pKho,@quan,2,DATEADD(day,-20,@now)),(@pKho,@ducanh,0,DATEADD(day,-19,@now)),
        (@pCampaign,@ha,2,DATEADD(day,-15,@now)),(@pCampaign,@mai,0,DATEADD(day,-14,@now));

    -- ===== Pages (ghi chú) =====
    INSERT INTO Pages (ProjectId,Title,Content,CreatedAt,UpdatedAt) VALUES
        (@pWeb, N'Tổng quan dự án Website TMĐT', N'Mục tiêu: hoàn thiện website bán hàng trong 2 tháng. Phạm vi gồm trang chủ, danh mục sản phẩm, giỏ hàng, thanh toán VNPay và trang quản trị đơn hàng.', DATEADD(day,-29,@now), DATEADD(day,-12,@now)),
        (@pWeb, N'Tài liệu API', N'Các endpoint chính: /api/products, /api/cart, /api/orders, /api/payment. Xác thực bằng JWT. Phản hồi JSON theo chuẩn REST.', DATEADD(day,-24,@now), DATEADD(day,-6,@now)),
        (@pMobile, N'Ghi chú họp Sprint 3', N'Thống nhất hoàn thành màn hình đăng nhập và đẩy thông báo đơn hàng trong sprint này. Demo cuối tuần cho ban giám đốc.', DATEADD(day,-8,@now), DATEADD(day,-8,@now)),
        (@pKho, N'Quy trình nhập xuất kho', N'Nhập kho: tạo phiếu nhập, kiểm đếm, cập nhật tồn. Xuất kho: kiểm tra tồn, tạo phiếu xuất, trừ tồn. Cảnh báo khi tồn dưới mức tối thiểu.', DATEADD(day,-18,@now), DATEADD(day,-18,@now)),
        (@pCampaign, N'Kịch bản nội dung tháng 7', N'Tập trung kênh Facebook và TikTok. Mỗi tuần 3 bài viết và 2 video ngắn. Phối hợp KOL trong tuần ra mắt.', DATEADD(day,-14,@now), DATEADD(day,-5,@now));

    -- ===== Tasks (Priority: 0=Thấp,1=TB,2=Cao | Status: 0=Todo,1=Doing,2=Done) =====
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Thiết kế giao diện trang chủ',N'Dựng layout trang chủ chuẩn responsive.',2,2,DATEADD(day,-20,@now),NULL,60,@pWeb,@mai,DATEADD(day,-29,@now));
    DECLARE @t1 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Tích hợp cổng thanh toán VNPay',N'Kết nối VNPay cho luồng thanh toán đơn hàng.',2,1,DATEADD(day,5,@now),DATEADD(minute,540,@today),90,@pWeb,@ducanh,DATEADD(day,-18,@now));
    DECLARE @t2 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Xây dựng API giỏ hàng',N'CRUD giỏ hàng và đồng bộ với người dùng.',1,1,DATEADD(day,3,@now),NULL,60,@pWeb,@ducanh,DATEADD(day,-15,@now));
    DECLARE @t3 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Kiểm thử luồng đặt hàng',N'Test từ thêm giỏ hàng đến thanh toán thành công.',1,0,DATEADD(day,-1,@now),NULL,60,@pWeb,@mai,DATEADD(day,-10,@now));
    DECLARE @t4 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Tối ưu tốc độ tải trang',N'Nén ảnh, lazy-load và cache phía trình duyệt.',0,0,DATEADD(day,12,@now),NULL,60,@pWeb,@quan,DATEADD(day,-8,@now));
    DECLARE @t5 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Dựng khung màn hình đăng nhập',N'Giao diện đăng nhập và đăng ký cho app mobile.',1,2,DATEADD(day,-12,@now),NULL,60,@pMobile,@mai,DATEADD(day,-22,@now));
    DECLARE @t6 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Push notification đơn hàng',N'Gửi thông báo trạng thái đơn hàng tới khách.',2,1,DATEADD(day,7,@now),DATEADD(minute,840,@today),60,@pMobile,@ducanh,DATEADD(day,-9,@now));
    DECLARE @t7 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Màn hình chi tiết sản phẩm',N'Hiển thị hình ảnh, mô tả, giá và nút mua.',1,0,DATEADD(day,9,@now),NULL,60,@pMobile,@mai,DATEADD(day,-6,@now));
    DECLARE @t8 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Thiết kế cơ sở dữ liệu kho',N'Mô hình bảng sản phẩm, tồn kho, phiếu nhập xuất.',2,2,DATEADD(day,-8,@now),NULL,60,@pKho,@quan,DATEADD(day,-16,@now));
    DECLARE @t9 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Báo cáo tồn kho theo tháng',N'Tổng hợp nhập xuất tồn và xuất file Excel.',1,0,DATEADD(day,14,@now),NULL,60,@pKho,@ducanh,DATEADD(day,-5,@now));
    DECLARE @t10 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Cảnh báo hàng sắp hết',N'Gửi cảnh báo khi tồn dưới mức tối thiểu.',0,0,DATEADD(day,-3,@now),NULL,60,@pKho,@quan,DATEADD(day,-4,@now));
    DECLARE @t11 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Lên kế hoạch nội dung mạng xã hội',N'Lập lịch bài đăng Facebook và TikTok cho chiến dịch.',2,1,DATEADD(day,4,@now),DATEADD(minute,630,@today),120,@pCampaign,@ha,DATEADD(day,-7,@now));
    DECLARE @t12 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Thiết kế ấn phẩm quảng cáo',N'Banner, poster và bộ ảnh cho chiến dịch.',1,0,DATEADD(day,6,@now),NULL,60,@pCampaign,@mai,DATEADD(day,-3,@now));
    DECLARE @t13 INT=SCOPE_IDENTITY();
    INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES (N'Liên hệ KOL/Influencer',N'Tìm và chốt hợp tác với KOL phù hợp.',1,2,DATEADD(day,-5,@now),NULL,60,@pCampaign,@ha,DATEADD(day,-10,@now));
    DECLARE @t14 INT=SCOPE_IDENTITY();

    -- ===== Comments =====
    INSERT INTO TaskComments (TaskItemId,UserId,Content,CreatedAt) VALUES
        (@t2,@quan,N'Nhớ kiểm thử cả luồng hoàn tiền nhé.',DATEADD(day,-3,@now)),
        (@t2,@ducanh,N'Đã chạy được sandbox VNPay, đang chờ key production.',DATEADD(day,-2,@now)),
        (@t4,@mai,N'Phát hiện lỗi khi áp mã giảm giá, đang điều tra.',DATEADD(day,-1,@now)),
        (@t7,@ha,N'Cần thống nhất nội dung thông báo với team marketing.',DATEADD(day,-2,@now)),
        (@t9,@quan,N'Đã chuẩn hóa schema và thêm index cho mã hàng.',DATEADD(day,-7,@now)),
        (@t12,@admin,N'Kế hoạch ổn, ưu tiên Facebook và TikTok trong tuần đầu.',DATEADD(day,-4,@now));

    -- ===== Notifications =====
    INSERT INTO Notifications (UserId,TaskItemId,Message,IsRead,CreatedAt) VALUES
        (@ducanh,@t2,N'Bạn được giao task: Tích hợp cổng thanh toán VNPay',0,DATEADD(day,-18,@now)),
        (@mai,@t4,N'Bạn được giao task: Kiểm thử luồng đặt hàng',0,DATEADD(day,-10,@now)),
        (@ducanh,@t7,N'Bạn được giao task: Push notification đơn hàng',0,DATEADD(day,-9,@now)),
        (@mai,@t8,N'Bạn được giao task: Màn hình chi tiết sản phẩm',1,DATEADD(day,-6,@now)),
        (@ha,@t12,N'Bạn được giao task: Lên kế hoạch nội dung mạng xã hội',0,DATEADD(day,-7,@now));

    -- ===== Activity logs =====
    INSERT INTO ActivityLogs (UserId,Action,EntityType,EntityId,Description,CreatedAt) VALUES
        (@quan,'Created','Project',@pWeb,N'Tạo project "Website Thương mại điện tử"',DATEADD(day,-30,@now)),
        (@ducanh,'Created','Task',@t2,N'Tạo task "Tích hợp cổng thanh toán VNPay"',DATEADD(day,-18,@now)),
        (@mai,'ChangedStatus','Task',@t1,N'Đổi trạng thái task "Thiết kế giao diện trang chủ": Đang làm → Hoàn thành',DATEADD(day,-20,@now)),
        (@quan,'Commented','Task',@t2,N'Bình luận trong task "Tích hợp cổng thanh toán VNPay"',DATEADD(day,-3,@now)),
        (@ha,'Created','Project',@pCampaign,N'Tạo project "Chiến dịch Ra mắt Q3"',DATEADD(day,-15,@now)),
        (@quan,'ChangedStatus','Task',@t9,N'Đổi trạng thái task "Thiết kế cơ sở dữ liệu kho": Đang làm → Hoàn thành',DATEADD(day,-8,@now)),
        (@ha,'Commented','Task',@t7,N'Bình luận trong task "Push notification đơn hàng"',DATEADD(day,-2,@now));

    COMMIT;
    PRINT N'Đã chèn dữ liệu mẫu thực tế thành công.';
END
GO
