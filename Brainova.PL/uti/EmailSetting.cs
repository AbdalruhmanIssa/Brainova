using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace Brainova.PL.uti
{
    public class EmailSetting : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSetting(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var host = Require("Smtp:Host");
            var port = int.Parse(Require("Smtp:Port"));
            var enableSsl = bool.Parse(Require("Smtp:EnableSsl"));
            var user = Require("Smtp:User");
            var pass = Require("Smtp:AppPassword");
            var from = Require("Smtp:From");

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, pass)
            };

            using var message = new MailMessage(from, email, subject, htmlMessage)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(message);
        }

        private string Require(string key)
        {
            var value = _configuration[key];

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"SMTP configuration '{key}' is missing. " +
                    $"Local development: dotnet user-secrets set \"{key}\" \"<value>\". " +
                    $"Production: set the {key.Replace(":", "__")} environment variable. " +
                    "See the Configuration section of README.md.");

            return value;
        }
    }
}
