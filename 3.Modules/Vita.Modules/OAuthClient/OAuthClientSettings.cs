using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Modules.OAuthClient.Internal;

namespace Vita.Modules.OAuthClient {

  public class OAuthClientSettings {
    public string RedirectUrl;
    public string EncryptionChannel;
    public IJsonDeserializer JsonDeserializer; // for Jwt token in OpenId connect
    public string RedirectResponseRedirectsTo;
    public string RedirectResponseText; 

    public OAuthClientSettings(string redirectUrl, IJsonDeserializer deserializer = null, string encryptionChannel = null, 
         string redirectResponseRedirectTo = null, string redirectResponseText = null) {
      RedirectUrl = redirectUrl; //it might be set later
      JsonDeserializer = deserializer ?? new JsonDeserializer();
      EncryptionChannel = encryptionChannel;
      RedirectResponseRedirectsTo = redirectResponseRedirectTo;
      RedirectResponseText = redirectResponseText;
    }

  }//settings class

}//ns
