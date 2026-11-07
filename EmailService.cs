using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

public interface IEmailService
{
    Task SendInvoiceAsync(string toEmail, string clientName, string invoiceDetailsHtml);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendInvoiceAsync(string toEmail, string clientName, string invoiceDetailsHtml)
    {
        var smtpSection = _config.GetSection("Smtp");
        var host = smtpSection["Server"];
        var port = int.Parse(smtpSection["Port"]);
        var username = smtpSection["Username"];
        var password = smtpSection["Password"];
        var senderEmail = smtpSection["SenderEmail"];
        var senderName = smtpSection["SenderName"];

        using (var client = new SmtpClient(host, port))
        {
            client.Credentials = new NetworkCredential(username, password);
            client.EnableSsl = true;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Comprobante de compra - PharmaStock",
                Body = $"<h2>¡Gracias por tu compra, {clientName}!</h2><p>Aquí tienes el detalle de tu factura:</p>{invoiceDetailsHtml}",
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);
            await client.SendMailAsync(mailMessage);
        }
    }
}