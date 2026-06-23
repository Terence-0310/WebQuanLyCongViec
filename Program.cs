using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Hubs;
using Cetee.Models;
using Cetee.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database (EF Core + SQL Server) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Cấu hình Email (SMTP/Gmail) và JWT ---
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// --- ASP.NET Core Identity (khóa int) ---
builder.Services.AddIdentity<User, Role>(options =>
    {
        // Chính sách mật khẩu gọn cho đồ án (tối thiểu 6 ký tự).
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Giữ định dạng băm PBKDF2 cũ (để mật khẩu hiện có vẫn đăng nhập được) và sinh claim theo app.
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, AppUserClaimsPrincipalFactory>();

// Đường dẫn cho cookie đăng nhập của Identity.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Home/Index";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Đăng nhập Google (nếu đã cấu hình) — đăng nhập qua cookie ngoài của Identity.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme; // callback mặc định: /signin-google
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
}

// --- Đăng ký các service nghiệp vụ ---
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IPageService, PageService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(); // Realtime (WebSocket) — đẩy thông báo & đồng bộ dữ liệu.

// Khi chạy sau reverse proxy (nginx/Cloudflare): tin header X-Forwarded-Proto/For
// để app biết request gốc là HTTPS (cần cho redirect URI Google OAuth, cookie Secure...).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear(); // chỉ nginx (localhost) kết nối tới Kestrel nên tin mọi proxy
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// --- Áp dụng migration và seed dữ liệu mẫu khi khởi động ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    await DbSeeder.SeedAsync(db, roleManager, userManager);

    // Dọn các tài khoản dùng thử bị bỏ quên (>1 ngày) — tránh tích tụ dữ liệu rác.
    var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await auth.CleanupStaleTrialsAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<RealtimeHub>("/hubs/realtime");

app.Run();
