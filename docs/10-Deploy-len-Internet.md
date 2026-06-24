# 🚀 Triển khai (Deploy) ứng dụng Cetee lên Internet — Hướng dẫn chi tiết

> Tài liệu này hướng dẫn đưa ứng dụng từ máy local ra **chạy thật trên Internet** với
> tên miền riêng. Mô hình triển khai thực tế của dự án: **VPS Ubuntu (Vultr) + .NET
> self-contained chạy bằng systemd + SQL Server trong Docker + nginx reverse proxy +
> Cloudflare (DNS/HTTPS)**.
>
> ⚠️ **An toàn:** Mọi mật khẩu, khóa, app password trong tài liệu này đều là **placeholder**
> (`<...>`). Tuyệt đối không commit secret thật lên git; chỉ đặt trong
> `appsettings.Production.json` (chmod 600) trên server.

---

## 0. Sơ đồ tổng thể

```
            Người dùng Internet
                    │  https://cetee.cloud
                    ▼
        ┌───────────────────────┐
        │      Cloudflare       │  DNS + SSL (HTTPS) + CDN
        └───────────┬───────────┘
                    │  http (port 80)
                    ▼
        ┌───────────────────────┐
        │   VPS Ubuntu (Vultr)  │
        │  ┌─────────────────┐  │
        │  │ nginx :80       │  │  reverse proxy
        │  └────────┬────────┘  │
        │           │ proxy → 127.0.0.1:5000
        │  ┌────────▼────────┐  │
        │  │ Kestrel (.NET)  │  │  systemd: cetee.service
        │  │  app Cetee      │  │  ASPNETCORE_ENVIRONMENT=Production
        │  └────────┬────────┘  │
        │           │ 127.0.0.1:1433
        │  ┌────────▼────────┐  │
        │  │ SQL Server 2022 │  │  Docker container: cetee-sql
        │  │  (Docker)       │  │  volume: cetee-sqldata
        │  └─────────────────┘  │
        └───────────────────────┘
```

**Vì sao kiến trúc này?**
- **nginx** đứng trước để nhận HTTP/HTTPS, phục vụ tốt nhiều kết nối, chuyển tiếp về app.
- **Kestrel** (web server của .NET) chỉ lắng nghe `127.0.0.1:5000` — **không lộ ra ngoài**.
- **systemd** giữ app luôn chạy (tự khởi động lại khi crash / reboot máy).
- **SQL Server trong Docker** để cài đặt gọn, dễ tách dữ liệu (volume), dễ xoá/dựng lại.
- **Cloudflare** lo tên miền + chứng chỉ HTTPS miễn phí, che IP gốc.

---

## 1. Chuẩn bị

| Cần có | Ghi chú |
|--------|---------|
| 1 VPS Ubuntu | Ví dụ Vultr 2 vCPU / ~3GB RAM (SQL Server cần ≥ 2GB). |
| 1 tên miền | Ví dụ mua ở iNET/inet.vn (`cetee.cloud`). |
| Tài khoản Cloudflare | Miễn phí — quản lý DNS + cấp HTTPS. |
| .NET SDK 10 trên máy build | Để `dotnet publish`. |
| Công cụ SSH/SCP | `ssh`, `scp` (Windows: dùng Git Bash hoặc PowerShell). |

> Ứng dụng **tự chạy migration + seed** khi khởi động (xem `Program.cs`), nên không cần
> cài SQL Manually từng bảng — chỉ cần có server SQL trống là app tự tạo schema.

---

## 2. Tạo và làm cứng (harden) VPS

### 2.1. Đăng nhập lần đầu & cập nhật

```bash
ssh root@<IP_SERVER>
apt update && apt upgrade -y
```

### 2.2. Đổi mật khẩu root mạnh & (khuyến nghị) tạo user riêng

```bash
passwd                       # đặt mật khẩu root mới, mạnh
# (tùy chọn) tạo user thường + cấp sudo thay vì dùng root trực tiếp
adduser deploy
usermod -aG sudo deploy
```

> 🔐 **Quan trọng:** không bao giờ dán mật khẩu server vào chat/issue/commit. Nếu lỡ lộ,
> phải **đổi lại ngay**.

### 2.3. Bật tường lửa (ufw)

```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
ufw status
```

Chỉ mở **22 (SSH), 80, 443**. Cổng app (5000) và SQL (1433) **không mở ra ngoài**.

---

## 3. Cài SQL Server bằng Docker

### 3.1. Cài Docker

```bash
apt install -y docker.io
systemctl enable --now docker
```

### 3.2. Chạy container SQL Server 2022

```bash
docker run -d --name cetee-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=<MAT_KHAU_SA_MANH>" \
  -e "MSSQL_PID=Express" \
  -p 127.0.0.1:1433:1433 \
  -v cetee-sqldata:/var/opt/mssql \
  --restart unless-stopped \
  mcr.microsoft.com/mssql/server:2022-latest
```

Giải thích cờ quan trọng:
- `-p 127.0.0.1:1433:1433` — chỉ bind **localhost**, SQL **không lộ ra Internet**.
- `-v cetee-sqldata:/var/opt/mssql` — dữ liệu nằm ở **volume**, xoá container vẫn còn dữ liệu.
- `--restart unless-stopped` — tự bật lại khi reboot.
- `MSSQL_SA_PASSWORD` — mật khẩu `sa`, phải **đủ mạnh** (≥ 8 ký tự, có hoa/thường/số/ký tự đặc biệt).

Kiểm tra:

```bash
docker ps                     # thấy cetee-sql đang Up
docker logs cetee-sql --tail 20
```

---

## 4. Cài .NET Runtime trên server (nếu publish framework-dependent)

Dự án dùng cách **self-contained** (đóng gói kèm runtime) nên server **không cần** cài
.NET. Nếu muốn nhẹ hơn (framework-dependent) thì cài runtime:

```bash
# Tùy chọn: chỉ khi KHÔNG publish self-contained
apt install -y aspnetcore-runtime-10.0
```

> Dự án này chọn **self-contained** cho đơn giản: chỉ cần copy thư mục publish lên là chạy.

---

## 5. Build & publish ứng dụng (trên máy local)

Từ thư mục dự án:

```bash
# Xoá file cấu hình dev để không lẫn secret/development settings vào bản publish
# (giữ appsettings.json gốc, KHÔNG mang appsettings.Development.json lên server)

dotnet publish -c Release -r linux-x64 --self-contained -o ./publish
```

Đóng gói và đẩy lên server:

```bash
tar -czf publish.tar.gz -C ./publish .
scp publish.tar.gz root@<IP_SERVER>:/tmp/
```

Trên server, giải nén vào thư mục triển khai:

```bash
mkdir -p /var/www/cetee
tar -xzf /tmp/publish.tar.gz -C /var/www/cetee
```

---

## 6. Cấu hình secret production (`appsettings.Production.json`)

Tạo file **chỉ trên server** (không commit), đặt quyền chặt:

```bash
nano /var/www/cetee/appsettings.Production.json
```

Nội dung (thay placeholder):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1,1433;Database=CeteeDb;User Id=sa;Password=<MAT_KHAU_SA_MANH>;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Email": {
    "Host": "smtp.gmail.com",
    "Port": 465,
    "User": "<gmail_cua_ban>@gmail.com",
    "Password": "<APP_PASSWORD_16_KY_TU>",
    "FromName": "Cetee",
    "FromAddress": "<gmail_cua_ban>@gmail.com"
  },
  "Jwt": {
    "Issuer": "Cetee",
    "Audience": "CeteeUsers",
    "Key": "<CHUOI_BI_MAT_DAI_DE_KY_JWT>",
    "ResetTokenMinutes": 15
  },
  "Authentication": {
    "Google": {
      "ClientId": "<...>.apps.googleusercontent.com",
      "ClientSecret": "GOCSPX-<...>"
    }
  }
}
```

Khoá quyền file (chỉ chủ đọc/ghi):

```bash
chmod 600 /var/www/cetee/appsettings.Production.json
```

> 🔐 Toàn bộ secret **chỉ** nằm ở file này (chmod 600). Không bao giờ đẩy lên git.
> `appsettings.json` (không secret) thì commit bình thường để biết "khung" cấu hình.

---

## 7. Chạy app bằng systemd

Tạo service:

```bash
nano /etc/systemd/system/cetee.service
```

Nội dung:

```ini
[Unit]
Description=Cetee web app
After=network.target docker.service

[Service]
WorkingDirectory=/var/www/cetee
ExecStart=/var/www/cetee/Cetee
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=cetee
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000

[Install]
WantedBy=multi-user.target
```

Cấp quyền & khởi động:

```bash
chown -R www-data:www-data /var/www/cetee
chmod +x /var/www/cetee/Cetee

systemctl daemon-reload
systemctl enable --now cetee
systemctl status cetee          # phải thấy active (running)
```

Xem log realtime (rất hữu ích khi gỡ lỗi):

```bash
journalctl -u cetee -f
```

> Khi app khởi động lần đầu, nó **tự migrate + seed** (tạo bảng + SuperAdmin
> `admin@example.com` / `Admin@123`). Đăng nhập xong nên **đổi mật khẩu** ngay.

---

## 8. Cài & cấu hình nginx (reverse proxy)

```bash
apt install -y nginx
nano /etc/nginx/sites-available/cetee
```

Nội dung:

```nginx
server {
    listen 80;
    server_name cetee.cloud www.cetee.cloud;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;

        # Cần cho WebSocket (SignalR realtime - thông báo & đồng bộ)
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        "upgrade";

        # Chuyển thông tin request gốc để app biết là HTTPS (cho cookie Secure, redirect Google OAuth)
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;

        proxy_cache_bypass $http_upgrade;
    }
}
```

Bật site & reload:

```bash
ln -s /etc/nginx/sites-available/cetee /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default     # bỏ site mặc định
nginx -t                                   # kiểm tra cú pháp
systemctl reload nginx
```

> 📌 **Liên hệ với code:** `Program.cs` đã cấu hình `UseForwardedHeaders()` để tin
> `X-Forwarded-Proto/For` từ nginx — nhờ vậy app biết request gốc là **HTTPS** (cần cho
> redirect URI của Google OAuth và cookie Secure). Header `Upgrade/Connection` ở trên là
> bắt buộc để **SignalR (WebSocket)** chạy được qua proxy.

---

## 9. Tên miền & HTTPS bằng Cloudflare

1. Đăng ký domain (vd ở inet.vn), trỏ **nameserver** về Cloudflare.
2. Trong Cloudflare → **DNS**, thêm bản ghi:
   - `A` | `@` (cetee.cloud) → `<IP_SERVER>` | Proxy **ON** (đám mây cam).
   - `A` | `www` → `<IP_SERVER>` | Proxy **ON**.
3. **SSL/TLS** → chọn chế độ **Flexible** (Cloudflare ↔ user là HTTPS; Cloudflare ↔ server
   là HTTP port 80).

> 📌 **Vì sao dùng Flexible & vì sao Program.cs KHÔNG có `UseHttpsRedirection`?**
> Cloudflare đã lo HTTPS cho người dùng. Server chỉ nói HTTP với Cloudflare. Nếu app tự
> ép redirect sang HTTPS sẽ tạo **vòng lặp chuyển hướng**. Do đó code cố tình **không**
> bật `UseHttpsRedirection` để Flexible chạy mượt. (Muốn bảo mật hơn thì dùng chế độ
> **Full** + chứng chỉ Origin của Cloudflare cài trên nginx — nâng cao.)

Sau bước này: mở `https://cetee.cloud` là thấy ứng dụng chạy thật. 🎉

---

## 10. Cập nhật Google OAuth cho domain thật

Vào Google Cloud Console → OAuth Client → thêm **Authorized redirect URI**:

```
https://cetee.cloud/signin-google
```

(Giữ cả URI localhost nếu vẫn muốn test ở máy.) Nếu không cập nhật, đăng nhập Google trên
domain thật sẽ báo `redirect_uri_mismatch`.

---

## 11. Quy trình cập nhật phiên bản mới (redeploy)

Mỗi lần sửa code, làm lại gọn:

```bash
# Trên máy local
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish
tar -czf publish.tar.gz -C ./publish .
scp publish.tar.gz root@<IP_SERVER>:/tmp/

# Trên server
tar -xzf /tmp/publish.tar.gz -C /var/www/cetee      # đè lên bản cũ
systemctl restart cetee                              # khởi động lại app
journalctl -u cetee -f                               # theo dõi log khởi động
```

> `appsettings.Production.json` nằm sẵn trong `/var/www/cetee` và **không** có trong gói
> publish nên không bị ghi đè khi cập nhật.

---

## 12. Sao lưu & phục hồi database

```bash
# Sao lưu: bung file .bak trong container rồi copy ra ngoài
docker exec cetee-sql /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P '<MAT_KHAU_SA_MANH>' \
  -Q "BACKUP DATABASE [CeteeDb] TO DISK='/var/opt/mssql/backup/cetee.bak'"
docker cp cetee-sql:/var/opt/mssql/backup/cetee.bak ./cetee-$(date +%F).bak
```

Hoặc đơn giản hơn: **dữ liệu nằm ở volume `cetee-sqldata`**, có thể backup cả volume.

---

## 13. Checklist khi gặp sự cố (troubleshooting)

| Triệu chứng | Cách kiểm tra |
|-------------|----------------|
| Mở web bị 502 Bad Gateway | App chưa chạy → `systemctl status cetee`, `journalctl -u cetee -f`. |
| App không lên, lỗi kết nối DB | `docker ps` xem `cetee-sql` Up chưa; kiểm tra connection string/mật khẩu sa. |
| Đăng nhập Google lỗi redirect | Thiếu `https://cetee.cloud/signin-google` trong Google Console. |
| Realtime (thông báo) không chạy | Thiếu header `Upgrade/Connection` trong nginx; kiểm tra WebSocket. |
| Vòng lặp chuyển hướng (ERR_TOO_MANY_REDIRECTS) | SSL Cloudflare để Flexible nhưng app lại ép HTTPS — đảm bảo không bật `UseHttpsRedirection`. |
| Không gửi được email OTP | Sai Gmail App Password; xem log `journalctl -u cetee`. |
| Trang lỗi 500 | Xem log app; thường do thiếu/sai `appsettings.Production.json`. |

Các lệnh hay dùng:

```bash
systemctl status cetee          # trạng thái app
systemctl restart cetee         # khởi động lại app
journalctl -u cetee -f          # log app realtime
docker ps                       # container SQL
docker logs cetee-sql --tail 50 # log SQL
nginx -t && systemctl reload nginx  # kiểm tra + nạp lại nginx
```

---

## 14. Tóm tắt các quyết định kiến trúc (để trả lời khi bảo vệ)

| Quyết định | Lý do |
|-----------|-------|
| Kestrel chỉ bind `127.0.0.1:5000` | Không lộ app ra ngoài; mọi traffic phải qua nginx. |
| nginx reverse proxy | Nhận HTTP/HTTPS, hỗ trợ WebSocket, ổn định nhiều kết nối. |
| systemd quản lý app | Tự khởi động lại khi crash/reboot; xem log tập trung. |
| SQL Server trong Docker, bind localhost | Cài gọn, tách dữ liệu (volume), không lộ DB ra Internet. |
| self-contained publish | Server không cần cài .NET; copy là chạy. |
| Cloudflare Flexible + không `UseHttpsRedirection` | HTTPS miễn phí cho người dùng, tránh vòng lặp redirect. |
| `UseForwardedHeaders` trong Program.cs | App nhận biết HTTPS gốc qua proxy (cookie Secure, OAuth redirect). |
| Secret ở `appsettings.Production.json` (chmod 600) | Tách bí mật khỏi mã nguồn; không commit lên git. |

---

## 15. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Vì sao không cho người dùng truy cập thẳng vào Kestrel (port 5000)?**
A: Kestrel tối ưu cho việc xử lý request .NET, nhưng nginx mạnh hơn ở việc tiếp nhận
kết nối từ Internet, TLS, nén, WebSocket. Đặt nginx phía trước là **best practice**.

**Q: Database đặt trong Docker có an toàn không?**
A: Có — container bind `127.0.0.1:1433` nên **không lộ ra Internet**, chỉ app trên cùng
máy kết nối được; dữ liệu lưu ở **volume** nên không mất khi cập nhật container.

**Q: Làm sao app tự tạo bảng khi deploy?**
A: `Program.cs` gọi migrate + `DbSeeder` khi khởi động — bản publish chạy lần đầu sẽ tự
tạo schema và tài khoản SuperAdmin, không cần chạy SQL thủ công.

**Q: Nếu server reboot thì sao?**
A: `systemctl enable cetee` và Docker `--restart unless-stopped` đảm bảo app + SQL **tự
bật lại** sau khi máy khởi động.

**Q: Cập nhật code mới có làm mất dữ liệu/cấu hình không?**
A: Không. Chỉ đè thư mục publish; `appsettings.Production.json` và database (volume) giữ
nguyên. Restart service là xong.

**Q: HTTPS lấy từ đâu, có tốn phí không?**
A: Cloudflare cấp **miễn phí**. Chế độ Flexible mã hoá đoạn người dùng ↔ Cloudflare;
muốn mã hoá đến tận server thì dùng Full + chứng chỉ Origin (nâng cao).
