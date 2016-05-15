using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Modules.Email;
using Vita.Modules.Notifications;
using Vita.Modules.TextTemplates;

namespace Vita.Modules.Email {

  public class EmailNotificationProvider : INotificationProvider {
    EntityApp _app;
    IEmailSendService _emailService;
    ITemplateTransformService _templateService; 

    public EmailNotificationProvider(EntityApp app) {
      _app = app;
    }

    #region INotificationProvider members
    public void Init(EntityApp app) {
      _emailService = _app.GetService<IEmailSendService>();
      Util.Check(_emailService != null, "EmailNotificationProvider: failed to retrieve {0} instance.", typeof(IEmailSendService));
      _templateService = _app.GetService<ITemplateTransformService>();
      Util.Check(_templateService != null,
        "EmailNotificationProvider: failed to retrieve {0} instance, add Template module to your app.", typeof(ITemplateTransformService));
    }

    public bool CanSend(NotificationMessage message) {
      return message.MediaType.Equals(NotificationMediaTypes.Email, StringComparison.InvariantCultureIgnoreCase);  
    }

    public async Task SendAsync(OperationContext context, NotificationMessage message) {
      Util.CheckNotEmpty(message.Recipients, "Recipient(s) not specified.");
      try {
        var session = context.OpenSystemSession();
        var subject = message.GetString("Subject") ?? GetTemplatedValue(session, message, "Subject");
        var body = message.GetString("Body") ?? GetTemplatedValue(session, message, "Body");
        Util.CheckNotEmpty(message.From, "Email From address not specified in message.");
        Util.CheckNotEmpty(subject, "Subject not specified or Subject template '{0}.Subject' not found.", message.Type);
        Util.CheckNotEmpty(body, "Email body not specified or Body template '{0}.Body' not found.", message.Type);
        message.Status = MessageStatus.Sending;
        var mail = new MailMessage(message.From, message.Recipients, subject, body);
        await _emailService.SendAsync(context, mail);
        message.Status = MessageStatus.Sent;
      } catch (Exception ex) {
        message.Status = MessageStatus.Error;
        message.Error = ex.ToLogString(); 
      }
    }

    public string GetTemplatedValue(IEntitySession session, NotificationMessage message, string part) {
      if (_templateService == null)
        return null;
      var templateName = message.Type + "." + message.MediaType + "." + part;
      //Try with template
      var template = _templateService.GetTemplate(session, templateName, message.Culture, null);
      if (template == null && message.Culture != "EN-US") 
        template = _templateService.GetTemplate(session, templateName, "EN-US"); //try again with EN-US
      Util.Check(template != null, "Template {0}, culture '{1}' not found in TextTemplate table.", templateName, message.Culture);  
      var text = _templateService.Transform(template, message.Parameters);
      return text;
    }

    #endregion

  }
}
