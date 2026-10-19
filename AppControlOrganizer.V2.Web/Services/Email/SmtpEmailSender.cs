//-----------------------------------------------------------------------
// <copyright file="SmtpEmailSender.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Email;

using AppControlOrganizer.V2.Web.Services.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
/// <summary>
/// SMTP configuration bound from the "Smtp" configuration section or environment variables (Smtp__Host, etc.).
/// </summary>
public sealed class SmtpOptions
{
  public string Host { get; set; } = string.Empty;
  public int Port { get; set; } = 587;
  public bool UseSsl { get; set; } = true;
  public string User { get; set; } = string.Empty;
  public string Password { get; set; } = string.Empty;
  public string FromEmail { get; set; } = string.Empty;
  public string FromName { get; set; } = "LifePlanner";
}

/// <summary>
/// Sends transactional emails via SMTP using MailKit. Implements Identity's IEmailSender.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
  private readonly SmtpOptions options;
  private readonly ILogger<SmtpEmailSender> logger;

  public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
  {
    this.options = options.Value;
    this.logger = logger;
  }

  public async Task SendEmailAsync(string email, string subject, string htmlMessage)
  {
    if (string.IsNullOrWhiteSpace(email))
    {
      throw new ArgumentException("Recipient email must not be empty.", nameof(email));
    }

    if (string.IsNullOrWhiteSpace(subject))
    {
      throw new ArgumentException("Subject must not be empty.", nameof(subject));
    }

    if (string.IsNullOrWhiteSpace(htmlMessage))
    {
      throw new ArgumentException("Message body must not be empty.", nameof(htmlMessage));
    }

    if (string.IsNullOrWhiteSpace(options.Host))
    {
      logger.LogWarning(
        "SMTP Host is not configured. Email was not sent. EmailHash={EmailHash}. SubjectHash={SubjectHash}.",
        LogSafe.Hash(email),
        LogSafe.Hash(subject));
      return;
    }

    MimeMessage message = new();
    message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
    message.To.Add(MailboxAddress.Parse(email));
    message.Subject = subject;
    message.Body = new TextPart("html") { Text = htmlMessage };


    using SmtpClient client = new SmtpClient();
    try
    {
      SecureSocketOptions socketOptions = options.UseSsl
        ? SecureSocketOptions.StartTls
        : SecureSocketOptions.None;

      await client.ConnectAsync(options.Host, options.Port, socketOptions);

      if (!string.IsNullOrWhiteSpace(options.User))
      {
        await client.AuthenticateAsync(options.User, options.Password);
      }

      await client.SendAsync(message);
      logger.LogInformation(
        "Email sent. EmailHash={EmailHash}. SubjectHash={SubjectHash}.",
        LogSafe.Hash(email),
        LogSafe.Hash(subject));
    }
    catch (Exception ex)
    {
      logger.LogError(
        ex,
        "Failed to send email. EmailHash={EmailHash}. SubjectHash={SubjectHash}.",
        LogSafe.Hash(email),
        LogSafe.Hash(subject));
      throw;
    }
    finally
    {
      if (client.IsConnected)
      {
        await client.DisconnectAsync(quit: true);
      }
    }
  }
}
