/* =====================================================================
   seed-data.sql — Dữ liệu mẫu thực tế cho Cetee (Website quản lý công việc)
   ---------------------------------------------------------------------
   Mô phỏng một công ty nhỏ theo phân cấp 4 tầng:
     SuperAdmin → Admin → Manager → User  (+ vài User độc lập)
   Kèm workspace/project có thành viên rõ ràng để minh họa mô hình công ty.

   CÁCH DÙNG (trong SSMS):
     1. Tạo schema trước: chạy `dotnet ef database update` hoặc chạy app 1 lần
        (Program.cs tự migrate khi khởi động).
     2. Mở file này trong SSMS, trỏ tới database CeteeDb, bấm Execute (F5).
   Script chỉ chèn dữ liệu nghiệp vụ khi DB CHƯA có Workspace nào (tránh trùng).
   => Để nạp lại từ đầu: xóa dữ liệu cũ (hoặc DROP/again `database update`) rồi chạy.

   Tài khoản đăng nhập (mật khẩu Admin@123 cho SuperAdmin, User@123 cho còn lại):
     SuperAdmin : admin@example.com
     Admin      : khoa.phan@cetee.vn (Kỹ thuật), chau.ngo@cetee.vn (Kinh doanh)
     Manager    : quan.le@cetee.vn (Dev), dang.vu@cetee.vn (QA), trang.do@cetee.vn (MKT)
     User       : ducanh.tran, mai.vo, huy.bui (đội Quân); nga.phan, bao.ly (đội Đăng);
                  nhi.ho, kiet.dinh (đội Trang)
     Độc lập    : ha.pham@cetee.vn, lam.trinh@cetee.vn
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

-- 1) Bảo đảm có các vai trò phân cấp SuperAdmin/Admin/Manager/User
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'SuperAdmin') INSERT INTO Roles(Name) VALUES (N'SuperAdmin');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Admin')      INSERT INTO Roles(Name) VALUES (N'Admin');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Manager')    INSERT INTO Roles(Name) VALUES (N'Manager');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'User')       INSERT INTO Roles(Name) VALUES (N'User');

DECLARE @superRole INT = (SELECT Id FROM Roles WHERE Name = 'SuperAdmin');
DECLARE @adminRole INT = (SELECT Id FROM Roles WHERE Name = 'Admin');
DECLARE @mgrRole   INT = (SELECT Id FROM Roles WHERE Name = 'Manager');
DECLARE @userRole  INT = (SELECT Id FROM Roles WHERE Name = 'User');

-- 2) Bảo đảm có tài khoản SuperAdmin bootstrap (chủ hệ thống, không bị xóa)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@example.com')
    INSERT INTO Users (FullName, Email, PasswordHash, RoleId, CreatedAt)
    VALUES (N'Quản trị hệ thống', 'admin@example.com', @adminHash, @superRole, @now);
ELSE
    UPDATE Users SET RoleId = @superRole, ManagerId = NULL WHERE Email = 'admin@example.com';

DECLARE @admin INT = (SELECT TOP 1 Id FROM Users WHERE Email = 'admin@example.com');

-- 3) Chỉ chèn dữ liệu mẫu khi DB chưa có Workspace nào
IF EXISTS (SELECT 1 FROM Workspaces)
BEGIN
    PRINT N'Đã có dữ liệu workspace — bỏ qua phần seed mẫu.';
    RETURN;
END

BEGIN TRAN;

-- =========================================================
-- USERS (phân cấp SuperAdmin → Admin → Manager → User)
-- =========================================================
-- Admin (trực thuộc SuperAdmin)
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Phan Anh Khoa','khoa.phan@cetee.vn',@adminHash,@adminRole,@admin,DATEADD(day,-60,@now));
DECLARE @aTech INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Ngô Bảo Châu','chau.ngo@cetee.vn',@adminHash,@adminRole,@admin,DATEADD(day,-58,@now));
DECLARE @aBiz INT=SCOPE_IDENTITY();

-- Manager (trực thuộc Admin)
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Lê Minh Quân','quan.le@cetee.vn',@userHash,@mgrRole,@aTech,DATEADD(day,-55,@now));
DECLARE @quan INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Vũ Hải Đăng','dang.vu@cetee.vn',@userHash,@mgrRole,@aTech,DATEADD(day,-54,@now));
DECLARE @dang INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Đỗ Thu Trang','trang.do@cetee.vn',@userHash,@mgrRole,@aBiz,DATEADD(day,-53,@now));
DECLARE @trang INT=SCOPE_IDENTITY();

-- User (trực thuộc Manager)
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Trần Đức Anh','ducanh.tran@cetee.vn',@userHash,@userRole,@quan,DATEADD(day,-50,@now));
DECLARE @ducanh INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Võ Thị Mai','mai.vo@cetee.vn',@userHash,@userRole,@quan,DATEADD(day,-49,@now));
DECLARE @mai INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Bùi Gia Huy','huy.bui@cetee.vn',@userHash,@userRole,@quan,DATEADD(day,-48,@now));
DECLARE @huy INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Phan Thị Nga','nga.phan@cetee.vn',@userHash,@userRole,@dang,DATEADD(day,-47,@now));
DECLARE @nga INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Lý Quốc Bảo','bao.ly@cetee.vn',@userHash,@userRole,@dang,DATEADD(day,-46,@now));
DECLARE @bao INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Hồ Yến Nhi','nhi.ho@cetee.vn',@userHash,@userRole,@trang,DATEADD(day,-45,@now));
DECLARE @nhi INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,ManagerId,CreatedAt) VALUES (N'Đinh Tuấn Kiệt','kiet.dinh@cetee.vn',@userHash,@userRole,@trang,DATEADD(day,-44,@now));
DECLARE @kiet INT=SCOPE_IDENTITY();

-- User độc lập (không trực thuộc ai — tự quản lý việc cá nhân)
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,CreatedAt) VALUES (N'Phạm Thu Hà','ha.pham@cetee.vn',@userHash,@userRole,DATEADD(day,-40,@now));
DECLARE @ha INT=SCOPE_IDENTITY();
INSERT INTO Users (FullName,Email,PasswordHash,RoleId,CreatedAt) VALUES (N'Trịnh Văn Lâm','lam.trinh@cetee.vn',@userHash,@userRole,DATEADD(day,-38,@now));
DECLARE @lam INT=SCOPE_IDENTITY();

-- =========================================================
-- WORKSPACES + thành viên (Role: 0=Member,1=Manager,2=Owner)
-- =========================================================
INSERT INTO Workspaces (Name,Description,OwnerId,CreatedAt) VALUES (N'Phòng Phát triển Sản phẩm', N'Đội kỹ thuật xây dựng và bảo trì các sản phẩm phần mềm của công ty.', @aTech, DATEADD(day,-40,@now));
DECLARE @wsDev INT=SCOPE_IDENTITY();
INSERT INTO Workspaces (Name,Description,OwnerId,CreatedAt) VALUES (N'Trung tâm Kiểm thử (QA)', N'Đảm bảo chất lượng: kiểm thử thủ công, tự động và hiệu năng.', @dang, DATEADD(day,-38,@now));
DECLARE @wsQA INT=SCOPE_IDENTITY();
INSERT INTO Workspaces (Name,Description,OwnerId,CreatedAt) VALUES (N'Marketing & Truyền thông', N'Quản lý chiến dịch quảng bá và nội dung truyền thông.', @aBiz, DATEADD(day,-36,@now));
DECLARE @wsMkt INT=SCOPE_IDENTITY();

INSERT INTO WorkspaceMembers (WorkspaceId,UserId,Role,JoinedAt) VALUES
    (@wsDev,@aTech,2,DATEADD(day,-40,@now)),(@wsDev,@quan,1,DATEADD(day,-39,@now)),(@wsDev,@dang,1,DATEADD(day,-39,@now)),
    (@wsDev,@ducanh,0,DATEADD(day,-38,@now)),(@wsDev,@mai,0,DATEADD(day,-38,@now)),(@wsDev,@huy,0,DATEADD(day,-37,@now)),
    (@wsDev,@nga,0,DATEADD(day,-36,@now)),(@wsDev,@bao,0,DATEADD(day,-36,@now)),
    (@wsQA,@dang,2,DATEADD(day,-38,@now)),(@wsQA,@quan,1,DATEADD(day,-37,@now)),(@wsQA,@nga,0,DATEADD(day,-37,@now)),(@wsQA,@bao,0,DATEADD(day,-36,@now)),
    (@wsMkt,@aBiz,2,DATEADD(day,-36,@now)),(@wsMkt,@trang,1,DATEADD(day,-35,@now)),(@wsMkt,@nhi,0,DATEADD(day,-35,@now)),(@wsMkt,@kiet,0,DATEADD(day,-34,@now));

-- =========================================================
-- PROJECTS + thành viên project (lấy từ thành viên workspace)
-- =========================================================
INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Website Thương mại điện tử', N'Website bán hàng trực tuyến: giỏ hàng, thanh toán, quản trị đơn.', @wsDev, DATEADD(day,-35,@now));
DECLARE @pWeb INT=SCOPE_IDENTITY();
INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Ứng dụng Mobile Bán hàng', N'App di động cho khách hàng đặt mua sản phẩm.', @wsDev, DATEADD(day,-30,@now));
DECLARE @pMobile INT=SCOPE_IDENTITY();
INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Hệ thống Quản trị Nội bộ', N'Quản lý kho, nhân sự và báo cáo nội bộ.', @wsDev, DATEADD(day,-26,@now));
DECLARE @pErp INT=SCOPE_IDENTITY();
INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Khung Kiểm thử Tự động', N'Bộ test tự động cho web và API.', @wsQA, DATEADD(day,-28,@now));
DECLARE @pAuto INT=SCOPE_IDENTITY();
INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Kiểm thử Hiệu năng', N'Đo tải, độ trễ và tối ưu hiệu năng hệ thống.', @wsQA, DATEADD(day,-22,@now));
DECLARE @pPerf INT=SCOPE_IDENTITY();
INSERT INTO Projects (Name,Description,WorkspaceId,CreatedAt) VALUES (N'Chiến dịch Ra mắt Q3', N'Kế hoạch truyền thông cho đợt ra mắt sản phẩm quý 3.', @wsMkt, DATEADD(day,-20,@now));
DECLARE @pCampaign INT=SCOPE_IDENTITY();

INSERT INTO ProjectMembers (ProjectId,UserId,Role,JoinedAt) VALUES
    (@pWeb,@quan,2,DATEADD(day,-35,@now)),(@pWeb,@ducanh,0,DATEADD(day,-34,@now)),(@pWeb,@mai,0,DATEADD(day,-34,@now)),(@pWeb,@huy,0,DATEADD(day,-33,@now)),
    (@pMobile,@mai,2,DATEADD(day,-30,@now)),(@pMobile,@ducanh,0,DATEADD(day,-29,@now)),(@pMobile,@huy,0,DATEADD(day,-29,@now)),
    (@pErp,@quan,2,DATEADD(day,-26,@now)),(@pErp,@bao,0,DATEADD(day,-25,@now)),
    (@pAuto,@dang,2,DATEADD(day,-28,@now)),(@pAuto,@nga,0,DATEADD(day,-27,@now)),(@pAuto,@bao,0,DATEADD(day,-27,@now)),
    (@pPerf,@nga,2,DATEADD(day,-22,@now)),(@pPerf,@bao,0,DATEADD(day,-21,@now)),
    (@pCampaign,@trang,2,DATEADD(day,-20,@now)),(@pCampaign,@nhi,0,DATEADD(day,-19,@now)),(@pCampaign,@kiet,0,DATEADD(day,-18,@now));

-- =========================================================
-- PAGES (ghi chú)
-- =========================================================
INSERT INTO Pages (ProjectId,Title,Content,CreatedAt,UpdatedAt) VALUES
    (@pWeb, N'Tổng quan dự án Website', N'Mục tiêu: hoàn thiện website bán hàng trong 2 tháng. Gồm trang chủ, danh mục, giỏ hàng, thanh toán VNPay và trang quản trị.', DATEADD(day,-34,@now), DATEADD(day,-10,@now)),
    (@pWeb, N'Tài liệu API', N'Endpoint chính: /api/products, /api/cart, /api/orders, /api/payment. Xác thực JWT, phản hồi JSON chuẩn REST.', DATEADD(day,-28,@now), DATEADD(day,-6,@now)),
    (@pMobile, N'Ghi chú họp Sprint', N'Hoàn thành màn đăng nhập và đẩy thông báo đơn hàng trong sprint. Demo cuối tuần cho ban giám đốc.', DATEADD(day,-12,@now), DATEADD(day,-12,@now)),
    (@pErp, N'Quy trình nhập xuất kho', N'Nhập: tạo phiếu, kiểm đếm, cập nhật tồn. Xuất: kiểm tra tồn, tạo phiếu, trừ tồn. Cảnh báo khi tồn dưới mức tối thiểu.', DATEADD(day,-20,@now), DATEADD(day,-20,@now)),
    (@pAuto, N'Chiến lược kiểm thử', N'Ưu tiên smoke test cho luồng mua hàng; bổ sung regression theo từng release.', DATEADD(day,-24,@now), DATEADD(day,-8,@now)),
    (@pCampaign, N'Kịch bản nội dung tháng 7', N'Tập trung Facebook và TikTok: mỗi tuần 3 bài viết, 2 video ngắn. Phối hợp KOL tuần ra mắt.', DATEADD(day,-18,@now), DATEADD(day,-5,@now));

-- =========================================================
-- TASKS (Priority 0/1/2 = Thấp/TB/Cao | Status 0/1/2 = Todo/Doing/Done)
-- Một số task có ScheduledStart (lịch ngày hôm nay) và có task quá hạn.
-- =========================================================
INSERT INTO Tasks (Title,Description,Priority,Status,DueDate,ScheduledStart,DurationMinutes,ProjectId,AssigneeId,CreatedAt) VALUES
-- pWeb
 (N'Thiết kế giao diện trang chủ',N'Layout trang chủ responsive.',2,2,DATEADD(day,-22,@now),NULL,60,@pWeb,@mai,DATEADD(day,-33,@now)),
 (N'Tích hợp cổng thanh toán VNPay',N'Kết nối VNPay cho luồng thanh toán.',2,1,DATEADD(day,5,@now),DATEADD(minute,540,@today),90,@pWeb,@ducanh,DATEADD(day,-20,@now)),
 (N'Xây dựng API giỏ hàng',N'CRUD giỏ hàng, đồng bộ người dùng.',1,1,DATEADD(day,3,@now),NULL,60,@pWeb,@huy,DATEADD(day,-16,@now)),
 (N'Kiểm thử luồng đặt hàng',N'Test từ thêm giỏ tới thanh toán.',1,0,DATEADD(day,-1,@now),NULL,60,@pWeb,@mai,DATEADD(day,-9,@now)),
-- pMobile
 (N'Dựng màn hình đăng nhập app',N'Đăng nhập và đăng ký cho mobile.',1,2,DATEADD(day,-14,@now),NULL,60,@pMobile,@mai,DATEADD(day,-24,@now)),
 (N'Push notification đơn hàng',N'Gửi trạng thái đơn hàng tới khách.',2,1,DATEADD(day,7,@now),DATEADD(minute,840,@today),60,@pMobile,@ducanh,DATEADD(day,-10,@now)),
 (N'Màn hình chi tiết sản phẩm',N'Hình ảnh, mô tả, giá, nút mua.',1,0,DATEADD(day,9,@now),NULL,60,@pMobile,@huy,DATEADD(day,-6,@now)),
-- pErp
 (N'Thiết kế cơ sở dữ liệu kho',N'Bảng sản phẩm, tồn kho, phiếu nhập xuất.',2,2,DATEADD(day,-8,@now),NULL,60,@pErp,@quan,DATEADD(day,-18,@now)),
 (N'Báo cáo tồn kho theo tháng',N'Tổng hợp nhập xuất tồn, xuất Excel.',1,0,DATEADD(day,14,@now),NULL,60,@pErp,@bao,DATEADD(day,-5,@now)),
 (N'Cảnh báo hàng sắp hết',N'Cảnh báo khi tồn dưới mức tối thiểu.',0,0,DATEADD(day,-3,@now),NULL,60,@pErp,@bao,DATEADD(day,-4,@now)),
-- pAuto
 (N'Dựng khung test tự động',N'Thiết lập framework cho web và API.',2,1,DATEADD(day,4,@now),DATEADD(minute,600,@today),120,@pAuto,@nga,DATEADD(day,-26,@now)),
 (N'Viết test luồng mua hàng',N'Smoke test cho luồng mua hàng chính.',1,1,DATEADD(day,6,@now),NULL,90,@pAuto,@bao,DATEADD(day,-15,@now)),
 (N'Tích hợp CI chạy test',N'Chạy test tự động trên mỗi commit.',1,0,DATEADD(day,10,@now),NULL,60,@pAuto,@nga,DATEADD(day,-7,@now)),
-- pPerf
 (N'Kịch bản kiểm thử tải',N'Mô phỏng 1000 người dùng đồng thời.',2,1,DATEADD(day,8,@now),NULL,90,@pPerf,@nga,DATEADD(day,-20,@now)),
 (N'Tối ưu truy vấn chậm',N'Phát hiện và tối ưu các truy vấn nặng.',2,0,DATEADD(day,-2,@now),NULL,60,@pPerf,@bao,DATEADD(day,-9,@now)),
-- pCampaign
 (N'Lên kế hoạch nội dung mạng xã hội',N'Lịch bài đăng Facebook và TikTok.',2,1,DATEADD(day,4,@now),DATEADD(minute,630,@today),120,@pCampaign,@nhi,DATEADD(day,-12,@now)),
 (N'Thiết kế ấn phẩm quảng cáo',N'Banner, poster, bộ ảnh chiến dịch.',1,0,DATEADD(day,6,@now),NULL,60,@pCampaign,@kiet,DATEADD(day,-6,@now)),
 (N'Liên hệ KOL/Influencer',N'Tìm và chốt hợp tác với KOL phù hợp.',1,2,DATEADD(day,-5,@now),NULL,60,@pCampaign,@nhi,DATEADD(day,-11,@now));

-- =========================================================
-- COMMENTS, NOTIFICATIONS, ACTIVITY LOGS (tham chiếu task theo tiêu đề)
-- =========================================================
DECLARE @tVnpay INT=(SELECT Id FROM Tasks WHERE Title=N'Tích hợp cổng thanh toán VNPay');
DECLARE @tOrder INT=(SELECT Id FROM Tasks WHERE Title=N'Kiểm thử luồng đặt hàng');
DECLARE @tPush  INT=(SELECT Id FROM Tasks WHERE Title=N'Push notification đơn hàng');
DECLARE @tDb    INT=(SELECT Id FROM Tasks WHERE Title=N'Thiết kế cơ sở dữ liệu kho');
DECLARE @tAuto  INT=(SELECT Id FROM Tasks WHERE Title=N'Dựng khung test tự động');
DECLARE @tSocial INT=(SELECT Id FROM Tasks WHERE Title=N'Lên kế hoạch nội dung mạng xã hội');

INSERT INTO TaskComments (TaskItemId,UserId,Content,CreatedAt) VALUES
    (@tVnpay,@quan,N'Nhớ kiểm thử cả luồng hoàn tiền nhé.',DATEADD(day,-3,@now)),
    (@tVnpay,@ducanh,N'Đã chạy sandbox VNPay, đang chờ key production.',DATEADD(day,-2,@now)),
    (@tOrder,@mai,N'Phát hiện lỗi khi áp mã giảm giá, đang điều tra.',DATEADD(day,-1,@now)),
    (@tAuto,@dang,N'Ưu tiên dựng khung trước, viết case sau.',DATEADD(day,-4,@now)),
    (@tDb,@quan,N'Đã chuẩn hóa schema và thêm index cho mã hàng.',DATEADD(day,-7,@now)),
    (@tSocial,@trang,N'Tuần đầu tập trung Facebook và TikTok.',DATEADD(day,-4,@now));

INSERT INTO Notifications (UserId,TaskItemId,Message,IsRead,CreatedAt) VALUES
    (@ducanh,@tVnpay,N'Bạn được giao task: Tích hợp cổng thanh toán VNPay',0,DATEADD(day,-20,@now)),
    (@mai,@tOrder,N'Bạn được giao task: Kiểm thử luồng đặt hàng',0,DATEADD(day,-9,@now)),
    (@ducanh,@tPush,N'Bạn được giao task: Push notification đơn hàng',0,DATEADD(day,-10,@now)),
    (@nga,@tAuto,N'Bạn được giao task: Dựng khung test tự động',1,DATEADD(day,-26,@now)),
    (@nhi,@tSocial,N'Bạn được giao task: Lên kế hoạch nội dung mạng xã hội',0,DATEADD(day,-12,@now));

INSERT INTO ActivityLogs (UserId,Action,EntityType,EntityId,Description,CreatedAt) VALUES
    (@quan,'Created','Project',@pWeb,N'Tạo project "Website Thương mại điện tử"',DATEADD(day,-35,@now)),
    (@ducanh,'Created','Task',@tVnpay,N'Tạo task "Tích hợp cổng thanh toán VNPay"',DATEADD(day,-20,@now)),
    (@mai,'ChangedStatus','Task',@tOrder,N'Đổi trạng thái task "Kiểm thử luồng đặt hàng": Cần làm → Đang làm',DATEADD(day,-8,@now)),
    (@quan,'Commented','Task',@tVnpay,N'Bình luận trong task "Tích hợp cổng thanh toán VNPay"',DATEADD(day,-3,@now)),
    (@dang,'Created','Project',@pAuto,N'Tạo project "Khung Kiểm thử Tự động"',DATEADD(day,-28,@now)),
    (@quan,'ChangedStatus','Task',@tDb,N'Đổi trạng thái task "Thiết kế cơ sở dữ liệu kho": Đang làm → Hoàn thành',DATEADD(day,-8,@now)),
    (@trang,'Created','Project',@pCampaign,N'Tạo project "Chiến dịch Ra mắt Q3"',DATEADD(day,-20,@now)),
    (@nhi,'Commented','Task',@tSocial,N'Bình luận trong task "Lên kế hoạch nội dung mạng xã hội"',DATEADD(day,-4,@now));

-- Phân loại tài khoản: Nhân viên công ty (1) cho người trong cơ cấu (vai trò quản lý
-- hoặc có cấp trên); Cá nhân (0, mặc định) cho User độc lập như ha.pham, lam.trinh.
UPDATE u SET u.AccountType = 1
FROM Users u INNER JOIN Roles r ON r.Id = u.RoleId
WHERE r.Name IN ('SuperAdmin','Admin','Manager') OR u.ManagerId IS NOT NULL;

-- Điền các trường ASP.NET Core Identity cho user mới chèn (để đăng nhập theo email).
UPDATE Users SET
    UserName = Email,
    NormalizedUserName = UPPER(Email),
    NormalizedEmail = UPPER(Email),
    EmailConfirmed = 1,
    SecurityStamp = CONVERT(nvarchar(36), NEWID()),
    ConcurrencyStamp = CONVERT(nvarchar(36), NEWID())
WHERE NormalizedUserName IS NULL;

COMMIT;
PRINT N'Đã chèn dữ liệu mẫu công ty (4 tầng + thành viên) thành công.';
GO
