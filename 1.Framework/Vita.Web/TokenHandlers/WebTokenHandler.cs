using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Api;

namespace Vita.Web {

  public enum WebTokenType {
    Header = 1, //token is in the header
    Cookie = 2, // token is in cookie
  }
  [Flags]
  public enum WebTokenDirection {
    None = 0,
    Input = 1,
    Output = 1 << 1,
    InputOutput = Input | Output,
  }

  /// <summary>Handles web tokens - incoming/outgoing headers (cookies).</summary>
  public class WebTokenHandler {
    public readonly string TokenName;
    public readonly WebTokenType TokenType;
    public readonly WebTokenDirection Direction;

    public WebTokenHandler(string tokenName, WebTokenType tokenType, WebTokenDirection direction) {
      TokenName = tokenName;
      TokenType = tokenType;
      Direction = direction; 
    }

    public virtual void HandleRequest(WebCallContext context, HttpRequestMessage request) {

    }
    public virtual void HandleResponse(WebCallContext context, HttpResponseMessage response) {

    }

    //Utilities
    protected string GetIncomingValue(WebCallContext context) {
      if (!Direction.IsSet(WebTokenDirection.Input)) //it is not input token
        return null; 
      switch (this.TokenType) {
        case WebTokenType.Cookie:
          var cookies = context.GetIncomingCookies(this.TokenName);
          if (cookies == null)
            return null; 
          if (cookies.Count == 1)
            return cookies[0].Value;
          return string.Join(";", cookies.Select(ck => ck.Value));

        case WebTokenType.Header:
          var values = context.GetIncomingHeader(this.TokenName);
          if (values == null || values.Count == 0)
            return null; 
          if (values.Count == 1)
            return values[0];
          return string.Join(";", values);
      }//switch
      return null; 
    }

    protected void SetOutgoingValue(WebCallContext context, string value) {
      if (!Direction.IsSet(WebTokenDirection.Output)) //it is not output token
        return;
      switch (this.TokenType) {
        case WebTokenType.Cookie:
          context.OutgoingCookies.Add(new System.Net.Cookie(this.TokenName, value));
          return;
        case WebTokenType.Header:
          context.OutgoingHeaders.Add(this.TokenName, value);
          return; 
      }//switch
    }

  }//class


}
