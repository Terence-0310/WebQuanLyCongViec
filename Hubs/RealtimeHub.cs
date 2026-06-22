using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cetee.Hubs;

/// <summary>
/// Hub realtime (SignalR). Server đẩy sự kiện xuống client:
///  - "notify"      : thông báo cá nhân (gửi tới đúng user qua Clients.User).
///  - "dataChanged" : dữ liệu nghiệp vụ thay đổi (task...) để các client đang xem tự đồng bộ.
/// Không cần method phía client gọi lên; chỉ nhận push. Yêu cầu đã đăng nhập.
/// </summary>
[Authorize]
public class RealtimeHub : Hub
{
}
