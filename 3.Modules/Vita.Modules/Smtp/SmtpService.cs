using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;

using Vita.Common; 
using Vita.Entities;
using Vita.Entities.Services.Implementations;
using Vita.Entities.Services;

namespace Vita.Modules.Smtp {

  public class SmtpSettings {
    public string Host; 
    public int Port = 25;
    public string UserName;
    public string Password;
    public string DefaultFrom = "donotreply@mysite.com";
    /// <summary>If not empty, redirects all emails to this address. For use in testing.</summary>
    public string TestRedirectAllTo; 
  }

  public class SmtpService : ISmtpService, IEntityService {
    EntityApp _app;
    SmtpSettings _settings;
    IErrorLogService _errorLog;

    public SmtpService(EntityApp app, SmtpSettings settings)  {
      _app = app; 
      _settings = settings;
      _app.RegisterService<ISmtpService>(this);
      _app.RegisterConfig(settings); 
    }

    #region IEntityServiceInit members
    public void Init(EntityApp app) {
      _errorLog = app.GetService<IErrorLogService>();
    }

    public void Shutdown() {
      
    }
    #endregion

    private SmtpClient CreateClient() {
      var client = new SmtpClient(_settings.Host, _settings.Port);
      client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
      return client; 
    }

    #region IEmailSendService
    public async Task SendAsync(OperationContext context, MailMessage message) {
      try {
        if (message.From == null)
          message.From = new MailAddress(_settings.DefaultFrom);
        if (!string.IsNullOrWhiteSpace(_settings.TestRedirectAllTo))
          SetupRedirect(message);
        var client = CreateClient();
        await client.SendMailAsync(message);
      } catch (Exception ex) {
        ex.Data["MailMessage"] = message.ToLogString();
        _errorLog.LogError(ex, context);
        throw;
      }
    }

    public void Send(OperationContext context, MailMessage message) {
      Task.Run(() => SendAsync(context, message)); 
    }

    private void SetupRedirect(MailMessage message) {
      var addrList = "To:" + string.Join(", ", message.To);
      if (message.CC.Count > 0)
        addrList += "; CC:" + string.Join(", ", message.CC);
      if (message.Bcc.Count > 0)
        addrList += "; BCC:" + string.Join(", ", message.Bcc);
      message.Subject = "(" + addrList + ")" + message.Subject;
      message.To.Clear();
      message.To.Add(_settings.TestRedirectAllTo);
      message.CC.Clear();
      message.Bcc.Clear(); 
    }
    #endregion


  }
}
