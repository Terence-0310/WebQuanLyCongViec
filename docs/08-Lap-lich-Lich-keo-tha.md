# Chức năng 8 — Lập lịch công việc & Lịch kéo thả (Timeline / Calendar Day-Week-Month)

## 1. Mục tiêu

Cho phép **xếp lịch** từng task vào một khung giờ cụ thể và xem dưới 3 chế độ:

- **Ngày (Timeline)**: trục giờ dọc, **kéo thả** task để xếp/đổi giờ, chỉnh thời lượng.
- **Tuần**: lưới 7 ngày (từ Thứ Hai), task xếp theo giờ từng ngày.
- **Tháng**: lưới 6 tuần để theo dõi tiến độ / "đếm công" cả kỳ.

Mỗi task có 2 trường phục vụ lịch: `ScheduledStart` (giờ bắt đầu) và `DurationMinutes`
(thời lượng → quyết định độ cao khối trên timeline).

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. "Xếp lịch" khác "deadline" thế nào?

| Trường | Ý nghĩa |
|--------|---------|
| `DueDate` | **Hạn chót** phải xong (mốc thời gian giới hạn). |
| `ScheduledStart` | **Dự định làm lúc nào** (đưa lên lịch để sắp xếp thời gian). |

Một task có thể có deadline nhưng **chưa xếp lịch** (`ScheduledStart == null`) → nằm ở
cột "chưa xếp lịch" để kéo vào timeline.

### 2.2. Kéo thả + AJAX

Khi kéo một task vào khung giờ, JS gửi **AJAX POST** tới `Schedule` để lưu giờ mới mà
**không tải lại trang**. Cùng cơ chế khi đổi thời lượng hoặc bỏ lịch.

### 2.3. Lịch là của từng người

Vì Timeline lọc theo người (qua `assigneeFilter`), một task đa phụ trách (Chức năng 7)
xuất hiện trên lịch của **mọi** người phụ trách. Đây là chủ ý, không phải lỗi.

---

## 3. Luồng hoạt động

```
TasksController.Timeline (GET ?date=&employeeId=)
   → TaskService.GetTimelineAsync → trả task ĐÃ xếp lịch trong ngày + task CHƯA xếp lịch

Kéo thả task vào khung giờ (JS)
   → POST TasksController.Schedule (id, start, duration)   [AJAX]
   → TaskService.ScheduleAsync → cập nhật ScheduledStart / DurationMinutes
   → trả JSON { ok = true } + đẩy realtime đồng bộ

TasksController.Week / Month → GetWeekAsync / GetMonthAsync → gom task theo từng ô ngày
```

---

## 4. Các file liên quan

| File | Vai trò |
|------|---------|
| [Controllers/TasksController.cs](../Controllers/TasksController.cs) | Action Timeline/Week/Month/Schedule. |
| [Services/TaskService.cs](../Services/TaskService.cs) | `GetTimelineAsync`, `GetWeekAsync`, `GetMonthAsync`, `ScheduleAsync`. |
| [Models/TaskItem.cs](../Models/TaskItem.cs) | `ScheduledStart`, `DurationMinutes`. |
| [ViewModels/TaskViewModels.cs](../ViewModels/TaskViewModels.cs) | `TimelineViewModel`, `WeekCalendarViewModel`, `MonthCalendarViewModel`, `CalendarDay`. |
| [Views/Tasks/Timeline.cshtml](../Views/Tasks/Timeline.cshtml) | Lịch ngày + JS kéo thả. |
| [Views/Tasks/Week.cshtml](../Views/Tasks/Week.cshtml) / [Month.cshtml](../Views/Tasks/Month.cshtml) | Lịch tuần / tháng. |

---

## 5. Giải thích code chính

### 5.1. Lịch ngày — task đã xếp & chưa xếp (`GetTimelineAsync`)

```csharp
var dayStart = date.Date;
var dayEnd   = dayStart.AddDays(1);

// Task ĐÃ xếp lịch trong ngày đang xem
var scheduled = await baseQuery
    .Where(t => t.ScheduledStart != null && t.ScheduledStart >= dayStart && t.ScheduledStart < dayEnd)
    .OrderBy(t => t.ScheduledStart).ToListAsync();

// Task CHƯA xếp lịch và chưa hoàn thành (để kéo vào timeline)
var unscheduled = await baseQuery
    .Where(t => t.ScheduledStart == null && t.Status != TaskStatus.Done)
    .OrderBy(t => t.DueDate).ToListAsync();
```

### 5.2. Xếp / đổi / bỏ lịch — `ScheduleAsync`

```csharp
public async Task<bool> ScheduleAsync(int id, bool changeStart, DateTime? start, int? duration,
    int userId, bool seeAll)
{
    var task = await Accessible(userId, seeAll).FirstOrDefaultAsync(t => t.Id == id);
    if (task is null) return false;

    if (changeStart) task.ScheduledStart = start;                       // start = null ⇒ BỎ lịch
    if (duration.HasValue) task.DurationMinutes = Math.Clamp(duration.Value, 5, 480); // 5'–8h
    await _db.SaveChangesAsync();

    if (changeStart && start.HasValue)                                  // chỉ log khi thực sự xếp/đổi giờ
        await _activity.LogAsync(userId, "Scheduled", "Task", task.Id,
            $"Xếp lịch task \"{task.Title}\" lúc {start.Value:HH:mm dd/MM} ({task.DurationMinutes} phút)");
    await BroadcastChangedAsync();
    return true;
}
```

### 5.3. Controller nhận lệnh kéo thả — `Schedule` (AJAX)

```csharp
// start KHÔNG gửi → giữ nguyên giờ; start "" → bỏ lịch; ngược lại "yyyy-MM-ddTHH:mm"
bool changeStart = start != null;
DateTime? when = null;
if (changeStart && start!.Length > 0)
{
    if (!DateTime.TryParse(start, out var parsed))
        return BadRequest(new { ok = false, message = "Thời gian không hợp lệ." });
    when = parsed;
}
var ok = await _tasks.ScheduleAsync(id, changeStart, when, duration, CurrentUserId, CanSeeAllData);
return ok ? Json(new { ok = true }) : NotFound(new { ok = false });
```

> Mẹo thiết kế hay: dùng `start == null` / `""` / có giá trị để phân biệt **3 hành động**
> (giữ nguyên / bỏ lịch / đặt giờ) chỉ với một tham số.

### 5.4. Tuần bắt đầu từ Thứ Hai — `WeekStartOf`

```csharp
private static DateTime WeekStartOf(DateTime d)
{
    int offset = ((int)d.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6 (theo thói quen VN)
    return d.Date.AddDays(-offset);
}
```

### 5.5. Lịch tháng — lưới 6 tuần (`GetMonthAsync`)

```csharp
var monthStart = new DateTime(date.Year, date.Month, 1);
var gridStart  = WeekStartOf(monthStart);   // lùi về Thứ Hai để lấp đầy lưới
var gridEnd    = gridStart.AddDays(42);      // 6 tuần × 7 ngày = 42 ô

var days = Enumerable.Range(0, 42).Select(i =>
{
    var day = gridStart.AddDays(i);
    return new CalendarDay
    {
        Date = day,
        InMonth = day.Month == monthStart.Month && day.Year == monthStart.Year, // tô mờ ngày ngoài tháng
        Tasks = tasks.Where(t => t.ScheduledStart!.Value.Date == day).ToList()
    };
}).ToList();
```

> Lưới luôn **42 ô** (6×7) để bố cục tháng nào cũng đều, ngày ngoài tháng tô mờ.

---

## 6. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Kéo thả lưu vào DB lúc nào?**
A: Ngay khi thả, JS gửi **AJAX POST** tới `Schedule`; Service cập nhật `ScheduledStart`
và `DurationMinutes` rồi trả JSON — **không reload trang**.

**Q: `DurationMinutes` dùng làm gì?**
A: Quyết định **độ cao khối task** trên timeline (1 phút ≈ vài px). Bị giới hạn 5–480
phút bằng `Math.Clamp`.

**Q: Sao tuần bắt đầu Thứ Hai mà không phải Chủ Nhật?**
A: Theo thói quen lịch Việt Nam. Công thức `((int)DayOfWeek + 6) % 7` đẩy Thứ Hai về 0.

**Q: Lịch tháng sao luôn đủ 42 ô?**
A: Bắt đầu từ Thứ Hai của tuần chứa ngày 1, lấy đúng 6 tuần (42 ngày) → bố cục đều mọi
tháng; ngày không thuộc tháng được tô mờ (`InMonth = false`).

**Q: Bỏ lịch một task làm sao?**
A: Gửi `start = ""` (rỗng) → `ScheduleAsync` set `ScheduledStart = null`, task quay lại
cột "chưa xếp lịch".

**Q: Vì sao chỉ ghi nhật ký khi đặt giờ mà không khi đổi thời lượng?**
A: Để nhật ký gọn, chỉ ghi hành động có ý nghĩa ("xếp lịch lúc..."), tránh spam log khi
chỉ kéo dài/ngắn khối.
