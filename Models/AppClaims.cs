namespace Cetee.Models;

/// <summary>Các loại claim tùy biến của ứng dụng (ngoài các claim chuẩn của Identity).</summary>
public static class AppClaims
{
    /// <summary>Loại tài khoản người dùng: "Personal" hoặc "Company".</summary>
    public const string AccountType = "account_type";
}
