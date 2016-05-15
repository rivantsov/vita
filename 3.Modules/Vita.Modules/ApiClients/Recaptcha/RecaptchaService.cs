using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.WebClient.Sync;
using Newtonsoft.Json;

namespace Vita.Modules.ApiClients.Recaptcha {

  public class RecaptchaService : IRecaptchaService {
    public const string ClientFaultCaptchaCheckFailed = "CaptchaCheckFailed";
    public const string RecaptchaUrl = " https://www.google.com/recaptcha/api/siteverify";
    static readonly DateTime UnixEra = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    RecaptchaSettings _settings;
    byte[] _encryptionKeyIv;

    //we allow setting recaptcha service later, at web app config time
    public RecaptchaService(RecaptchaSettings settings) {
      Util.Check(settings != null, "Recaptcha settings may not be null.");
      Util.Check(!string.IsNullOrEmpty(settings.SiteSecret), "reCaptcha not configured: SiteSecret may not be empty.");
      Util.Check(!string.IsNullOrEmpty(settings.SiteKey), "reCaptcha not configured: SiteKey may not be empty.");
      _settings = settings;
      if (_settings.Options.IsSet(RecaptchaOptions.UseSecureToken)) {
        _encryptionKeyIv = RecaptchaUtil.GetEncryptionKey(_settings.SiteSecret);
      }
    } //constructor

    #region IRecaptchaService members

    public string GetSiteKey() {
      return _settings.SiteKey;
    }

    public string GetSecretToken() {
      Util.Check(_settings.Options.IsSet(RecaptchaOptions.UseSecureToken), "Using secure token with reCapcha is not configured, cannot create token.");
      var sessionId = Guid.NewGuid(); 
      var timeStamp = (long)(DateTime.UtcNow - UnixEra).TotalMilliseconds;
      var token = RecaptchaUtil.GenerateSecureToken(sessionId, timeStamp, _encryptionKeyIv);
      return token; 
    }

    public bool Verify(string response, string clientIp = null, bool throwIfFail = true) {
      RecaptchaResponse resp; 
      var client = new WebClient.WebApiClient(RecaptchaUrl);
      if (_settings.Options.IsSet(RecaptchaOptions.CheckClientIp)) {
        Util.Check(!string.IsNullOrWhiteSpace(clientIp), "Client IP must be provided to verify the captcha.");
        resp = client.ExecutePost<object, RecaptchaResponse>(null, "?secret={0}&response={1}&remoteip={2}", _settings.SiteSecret, response, clientIp);
      } else 
        resp = client.ExecutePost<object, RecaptchaResponse>(null, "?secret={0}&response={1}", _settings.SiteSecret, response);
      if(resp.Success)
        return true;
      // if either no errors or a single error with just message about failed check, it is just wrong value; 
      if(resp.Errors != null || (resp.Errors.Length == 1 && resp.Errors[0] == "invalid-input-response")) {
        //return false or throw client fault
        if(throwIfFail)
          throw new ClientFaultException(ClientFaultCaptchaCheckFailed, "Captcha check failed.");
        else
          return false; 
      }
      //Fatal error
      Util.Check(resp.Errors != null && resp.Errors.Length > 0, "Recaptch check failed but no error messages returned.");
      var errMsg = "Captcha errors: " + string.Join("; ", resp.Errors) + "; User response: " + response;
      Util.Throw(errMsg);
      return false; //never happens
    }

    #endregion

  }//class
}//ns
