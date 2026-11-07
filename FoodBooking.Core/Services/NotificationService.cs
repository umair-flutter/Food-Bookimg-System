using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FoodBooking.Core.Services;

/// <summary>Email (SMTP) and SMS (Twilio). Every attempt is written to NotificationLogs so the admin can review it.</summary>
public class NotificationService(AppDbContext db, SettingsService settings, IHttpClientFactory http, ILogger<NotificationService> log)
{
    public async Task<bool> SendEmailAsync(string? to, string subject, string html)
    {
        if (string.IsNullOrWhiteSpace(to)) return false;
        var s = await settings.GetAsync();
        var entry = new NotificationLog { Channel = NotificationChannel.Email, Recipient = to, Subject = subject, Body = html };
        try
        {
            if (!s.EmailEnabled || string.IsNullOrWhiteSpace(s.SmtpHost))
            {
                entry.Error = "Email sending is disabled in Settings > Email & SMS (message logged only).";
            }
            else
            {
                using var client = new SmtpClient(s.SmtpHost, s.SmtpPort) { EnableSsl = s.SmtpUseSsl };
                if (!string.IsNullOrWhiteSpace(s.SmtpUser)) client.Credentials = new NetworkCredential(s.SmtpUser, s.SmtpPassword);
                var from = s.SmtpFromEmail ?? s.SmtpUser ?? s.Email ?? "noreply@localhost";
                using var msg = new MailMessage(new MailAddress(from, s.CompanyName), new MailAddress(to))
                {
                    Subject = subject, Body = html, IsBodyHtml = true, BodyEncoding = Encoding.UTF8
                };
                await client.SendMailAsync(msg);
                entry.Success = true;
            }
        }
        catch (Exception ex)
        {
            entry.Error = Trim(ex.Message);
            log.LogWarning(ex, "Email to {To} failed", to);
        }
        await SaveLogAsync(entry);
        return entry.Success;
    }

    public async Task<bool> SendSmsAsync(string? to, string message)
    {
        if (string.IsNullOrWhiteSpace(to)) return false;
        var s = await settings.GetAsync();
        var entry = new NotificationLog { Channel = NotificationChannel.Sms, Recipient = to, Subject = "SMS", Body = message };
        try
        {
            if (!s.SmsEnabled || string.IsNullOrWhiteSpace(s.TwilioAccountSid) || string.IsNullOrWhiteSpace(s.TwilioAuthToken))
            {
                entry.Error = "SMS sending is disabled in Settings > Email & SMS (message logged only).";
            }
            else
            {
                var client = http.CreateClient();
                var req = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api.twilio.com/2010-04-01/Accounts/{s.TwilioAccountSid}/Messages.json")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["To"] = to, ["From"] = s.TwilioFromNumber ?? "", ["Body"] = message
                    })
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{s.TwilioAccountSid}:{s.TwilioAuthToken}")));
                var res = await client.SendAsync(req);
                if (res.IsSuccessStatusCode) entry.Success = true;
                else entry.Error = Trim($"Twilio {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            entry.Error = Trim(ex.Message);
            log.LogWarning(ex, "SMS to {To} failed", to);
        }
        await SaveLogAsync(entry);
        return entry.Success;
    }

    public async Task LogAsync(NotificationChannel channel, string? recipient, string subject, string? body, bool success, string? error)
        => await SaveLogAsync(new NotificationLog { Channel = channel, Recipient = recipient, Subject = subject, Body = body, Success = success, Error = Trim(error) });

    private async Task SaveLogAsync(NotificationLog entry)
    {
        try { db.NotificationLogs.Add(entry); await db.SaveChangesAsync(); }
        catch (Exception ex) { log.LogError(ex, "Could not write notification log"); }
    }

    private static string? Trim(string? s) => s is { Length: > 1000 } ? s[..1000] : s;
}
