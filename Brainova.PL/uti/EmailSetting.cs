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
            var host = _configuration["Smtp:Host"];
            var port = int.Parse(_configuration["Smtp:Port"]!);
            var enableSsl = bool.Parse(_configuration["Smtp:EnableSsl"]!);
            var user = _configuration["Smtp:User"];
            var pass = _configuration["Smtp:AppPassword"];
            var from = _configuration["Smtp:From"];

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

    }
}
