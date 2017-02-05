using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.WebClient {

  public class WebApiClientErrorEventArgs : EventArgs {
    public readonly WebApiClient Client; 
    public readonly HttpResponseMessage Response;
    public Exception Exception;
    public bool Throw; 
    public WebApiClientErrorEventArgs(WebApiClient client, HttpResponseMessage response, Exception exception) {
      Client = client;
      Response = response;
      Exception = exception;
      Throw = true; 
    }
  }


  public partial class WebApiClient {

    public event EventHandler<WebApiClientErrorEventArgs> Error;

    public virtual async Task<Exception> ReadErrorResponseAsync(HttpResponseMessage response) {
      try {
        var hasBody = response.Content != null && response.Content.Headers.ContentLength > 0;
        if(!hasBody)
          return new ApiException("Web API call failed, no details returned. HTTP Status: " + response.StatusCode, response.StatusCode);
        switch(response.StatusCode) {
          case HttpStatusCode.BadRequest:
            if(Settings.BadRequestContentType == typeof(string))
              return await ReadErrorResponseUntypedAsync(response);
            var serErrors = await Settings.ContentSerializer.DeserializeAsync(Settings.BadRequestContentType, response.Content);
            if(Settings.ThrowClientFaultOnBadRequest) {
              var faults = (List<ClientFault>)serErrors.Object;
              return new ClientFaultException(faults);
            } else
              return new BadRequestException(serErrors.Object);
          default:
            if(Settings.ServerErrorContentType == typeof(string))
              return await ReadErrorResponseUntypedAsync(response);
            //deserialize custom object
            try {
              var serErr = await Settings.ContentSerializer.DeserializeAsync(Settings.ServerErrorContentType, response.Content);
              return new ApiException("Server error: " + serErr.Raw, response.StatusCode, serErr.Object);
            } catch(Exception ex) {
              var remoteErr = await response.Content.SafeReadContent();
              var msg = StringHelper.SafeFormat("Remote server error: {0}\r\n !!! Failed to deserialize response into error object, exc: {1}", remoteErr, ex.ToLogString());
              return new ApiException(msg, response.StatusCode, remoteErr);
            }
        }//switch 
      } catch(Exception exc) {
        Type errorType = response.StatusCode == System.Net.HttpStatusCode.BadRequest ? Settings.BadRequestContentType : Settings.ServerErrorContentType;
        var explain = StringHelper.SafeFormat("Failed to read error response returned from the service. \r\n" +
          "Expected content type: {0}. Consider changing it to match the error response for remote service. Deserializer error: {1}", errorType, exc.Message);
        throw new Exception(explain, exc);
      }
    }

    public async Task<ApiException> ReadErrorResponseUntypedAsync(HttpResponseMessage response) {
      var content = await response.Content.ReadAsStringAsync();
      string message, details; //if multiline, split
      SplitErrorMessage(content, out message, out details);
      return new ApiException(message, response.StatusCode, details);
    }

    private void SplitErrorMessage(string message, out string firstLine, out string others) {
      firstLine = message;
      others = null;
      if(string.IsNullOrWhiteSpace(message))
        return;
      var nlPos = message.IndexOf('\n');
      if(nlPos < 0)
        return;
      firstLine = message.Substring(0, nlPos);
      others = message.Substring(nlPos + 1);
    }


  }//class
}//ns
