using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers;

/// <summary>Đăng ký, đăng nhập (cookie Identity + Google), đăng xuất, quên mật khẩu.</summary>
public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly IPasswordResetService _reset;
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;

    public AccountController(
        IAuthService auth,
        IPasswordResetService reset,
        SignInManager<User> signInManager,
        UserManager<User> userManager)
    {
        _auth = auth;
        _reset = reset;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    // Request gửi từ modal (fetch) có header này -> trả JSON thay vì View.
    private bool IsAjax =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private string FirstError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrEmpty(m)) ?? "Dữ liệu không hợp lệ.";

    private JsonResult JsonError(string message) => Json(new { ok = false, error = message });

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("App", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return IsAjax ? JsonError(FirstError()) : View(model);

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null)
        {
            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, isPersistent: true, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                var url = (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    ? model.ReturnUrl
                    : Url.Action("App", "Home")!;
                return IsAjax ? Json(new { ok = true, redirect = url }) : Redirect(url);
            }
        }

        const string err = "Email hoặc mật khẩu không đúng.";
        if (IsAjax) return JsonError(err);
        ModelState.AddModelError(string.Empty, err);
        return View(model);
    }

    // Dùng thử miễn phí: tạo tài khoản dùng thử + dữ liệu mẫu rồi đăng nhập luôn (1 chạm).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trial()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("App", "Home");

        var user = await _auth.CreateTrialAsync();
        await _signInManager.SignInAsync(user, isPersistent: true);
        TempData["Success"] = "Chào mừng đến với bản dùng thử Cetee! Một workspace mẫu đã được tạo sẵn để bạn khám phá.";
        return RedirectToAction("Index", "Workspaces");
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("App", "Home");

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return IsAjax ? JsonError(FirstError()) : View(model);

        var (success, error, user) = await _auth.RegisterAsync(model);
        if (!success)
        {
            if (IsAjax) return JsonError(error!);
            ModelState.AddModelError(string.Empty, error!);
            return View(model);
        }

        await _signInManager.SignInAsync(user!, isPersistent: true);
        var url = Url.Action("App", "Home")!;
        return IsAjax ? Json(new { ok = true, redirect = url }) : Redirect(url);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ---------------------------------------------------------------------
    // Đăng nhập bằng Google (OAuth2). Cần cấu hình Authentication:Google trong appsettings.
    // ---------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> ExternalLogin(
        [FromServices] IAuthenticationSchemeProvider schemes,
        string provider = "Google", string? returnUrl = null)
    {
        if (await schemes.GetSchemeAsync(provider) is null)
        {
            TempData["Info"] = "Đăng nhập Google chưa được cấu hình (thiếu Client ID/Secret).";
            return RedirectToAction(nameof(Login));
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var props = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(props, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError is not null)
        {
            TempData["Info"] = "Đăng nhập Google thất bại: " + remoteError;
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            TempData["Info"] = "Không lấy được thông tin đăng nhập từ Google.";
            return RedirectToAction(nameof(Login));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var name = info.Principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Info"] = "Tài khoản Google không cung cấp email.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _auth.FindOrCreateExternalUserAsync(email, name ?? email);
        await _signInManager.SignInAsync(user, isPersistent: true);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("App", "Home");
    }

    // ---------------------------------------------------------------------
    // Quên mật khẩu: nhập email -> nhận OTP qua Gmail -> xác minh -> đổi mật khẩu.
    // ---------------------------------------------------------------------
    [HttpGet]
    public IActionResult ForgotPassword(string? email) =>
        View(new ForgotPasswordViewModel { Email = email ?? string.Empty });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        await _reset.RequestOtpAsync(model.Email);
        TempData["Info"] = "Nếu email tồn tại trong hệ thống, mã OTP đã được gửi tới hộp thư.";
        return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
    }

    [HttpGet]
    public IActionResult VerifyOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(ForgotPassword));
        return View(new VerifyOtpViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (ok, error, token) = await _reset.VerifyOtpAsync(model.Email, model.Code);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error!);
            return View(model);
        }

        TempData["ResetToken"] = token;
        return RedirectToAction(nameof(ResetPassword));
    }

    [HttpGet]
    public IActionResult ResetPassword()
    {
        var token = TempData["ResetToken"] as string;
        if (string.IsNullOrEmpty(token)) return RedirectToAction(nameof(ForgotPassword));

        TempData.Keep("ResetToken");
        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (ok, error) = await _reset.ResetPasswordAsync(model.Token, model.NewPassword);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error!);
            return View(model);
        }

        TempData["Info"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }
}
