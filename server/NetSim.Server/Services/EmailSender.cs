using MailKit.Net.Smtp;                    // SmtpClient - talks to a mail server
using MailKit.Security;                    // SecureSocketOptions - how the connection to the mail server is encrypted
using MimeKit;                             // MimeMessage, MailboxAddress, TextPart - the parts of an email
using Microsoft.Extensions.Configuration;  // IConfiguration - the server's settings

namespace NetSim.Server.Services;

// Sends real emails through SMTP - a thin wrapper around MailKit, so that the rest of the code (AuthService)
// doesn't need to know anything about SMTP/MailKit itself, only to call SendAsync
public class EmailSender
{
    // The server's settings; the mail server's address, user name and password are read from here
    private readonly IConfiguration _config;

    // The constructor. Dependency injection passes in the settings
    public EmailSender(IConfiguration config)
    {
        _config = config;
    }

    // Sends one plain-text email.
    // toEmail = the address to send to, subject = the title of the email, body = its text
    public async Task SendAsync(string toEmail, string subject, string body)
    {
        // An empty email
        var message = new MimeMessage();

        // Who the email is from: a display name and an address, both taken from the settings.
        // "??" = if no display name is configured, use "NetSim"
        message.From.Add(new MailboxAddress(_config["Smtp:FromName"] ?? "NetSim", _config["Smtp:FromEmail"]));

        // Who the email goes to. Parse turns the text into an address object (and throws if it is not an address)
        message.To.Add(MailboxAddress.Parse(toEmail));

        // The title of the email
        message.Subject = subject;

        // The content: "plain" = simple text, not HTML
        message.Body = new TextPart("plain") { Text = body };

        // The object that talks to the mail server. "using var" = it is closed automatically when the method ends
        using var client = new SmtpClient();

        // Connect to the mail server. Host and port come from the settings; int.Parse turns the port text
        // into a number ("!" = we promise the setting exists). StartTls = switch to an encrypted connection
        // right after connecting, so the password and the email do not travel as readable text
        await client.ConnectAsync(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]!), SecureSocketOptions.StartTls);

        // Log in to the mail server with our user name and password
        await client.AuthenticateAsync(_config["Smtp:Username"], _config["Smtp:Password"]);

        // Hand the email to the mail server
        await client.SendAsync(message);

        // Say goodbye to the mail server properly (true = send the "quit" command first) and disconnect
        await client.DisconnectAsync(true);
    }
}
