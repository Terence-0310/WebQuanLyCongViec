using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using Cetee.Models;

namespace Cetee.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
    Task SendOtpAsync(string toEmail, string toName, string code, int minutes);
}

/// <summary>Gửi email qua SMTP (Gmail) bằng MailKit. Hỗ trợ cổng 465 (SSL) và 587 (STARTTLS).</summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _s;
    private readonly ILogger<EmailService> _log;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> log)
    {
        _s = settings.Value;
        _log = log;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_s.FromName, _s.FromAddress));
        msg.To.Add(new MailboxAddress(toName, toEmail));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socket = _s.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_s.Host, _s.Port, socket);
        // App password của Gmail có thể chứa dấu cách khi hiển thị — bỏ đi trước khi xác thực.
        await client.AuthenticateAsync(_s.User, _s.Password.Replace(" ", ""));
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);

        _log.LogInformation("Đã gửi email tới {Email} (chủ đề: {Subject}).", toEmail, subject);
    }

    public Task SendOtpAsync(string toEmail, string toName, string code, int minutes)
    {
        string html = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto;border:1px solid #eceef3;border-radius:12px;overflow:hidden'>
  <div style='background:#1f2a44;color:#fff;padding:18px 24px;font-size:18px;font-weight:600'>Cetee — Đặt lại mật khẩu</div>
  <div style='padding:24px'>
    <p>Xin chào <b>{System.Net.WebUtility.HtmlEncode(toName)}</b>,</p>
    <p>Bạn (hoặc ai đó) vừa yêu cầu đặt lại mật khẩu. Mã xác nhận (OTP) của bạn là:</p>
    <div style='font-size:32px;font-weight:700;letter-spacing:8px;text-align:center;color:#1f2a44;background:#f3f6fb;border-radius:10px;padding:16px;margin:16px 0'>{code}</div>
    <p>Mã có hiệu lực trong <b>{minutes} phút</b>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
    <p style='color:#8a93a6;font-size:13px'>Nếu bạn không yêu cầu, hãy bỏ qua email này — mật khẩu của bạn vẫn an toàn.</p>
  </div>
</div>";
        return SendAsync(toEmail, toName, "Mã OTP đặt lại mật khẩu Cetee", html);
    }
}
