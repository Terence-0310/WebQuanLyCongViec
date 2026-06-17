namespace Cetee.Models;

/// <summary>Cấu hình gửi email qua SMTP (Gmail). Bind từ section "Email" trong appsettings.</summary>
public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 465;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "Cetee";
    public string FromAddress { get; set; } = string.Empty;
}
