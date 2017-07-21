using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Smtp {

  // SmtpService implements this interface
  public interface ISmtpService {
    Task SendAsync(OperationContext context, MailMessage message);
    void Send(OperationContext context, MailMessage message); 
  }
  /*
  // There is no default implementation for SMS service - use one of the SMS providers available on the Web. 
  // For a single mobile carrier, you can implement SMS by sending email to specific gateway. 
  // For Verizon, send email to 'phone@vtext.com', where phone is just digits, with dashes (-) removed. Leave subject empty.
  // Body is the SMS text itself. 
  public interface ISmsSendService {
    Task SendSmsAsync(OperationContext context, string phone, string sms);
    void SendSms(OperationContext context, string phone, string sms);
  }
  */
}
