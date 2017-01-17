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

  public class DefaultClientErrorHandler : IClientErrorHandler {
    public IContentSerializer Serializer;
    public readonly Type BadRequestContentType;
    public readonly Type ServerErrorContentType;
    public DefaultClientErrorHandler(IContentSerializer serializer, Type badRequestContentType = null, Type serverErrorContentType = null) {
      Serializer = serializer; 
      BadRequestContentType = badRequestContentType ?? typeof(List<ClientFault>);
      ServerErrorContentType = serverErrorContentType ?? typeof(string); 
    }

    #region IClientErrorHandler

    public virtual async Task<Exception> HandleErrorAsync(HttpResponseMessage response) {
      try {
        if(response.Content != null && response.Content.Headers.ContentLength > 0)
          return await ReadError(response);
        else
          return new ApiException("Web API call failed, no details returned. HTTP Status: " + response.StatusCode, response.StatusCode);
      } catch (Exception exc) {
        Type errorType = response.StatusCode == System.Net.HttpStatusCode.BadRequest ? BadRequestContentType : ServerErrorContentType;
        var explain = StringHelper.SafeFormat("Failed to read error response returned from the service. \r\n" +
          "Expected content type: {0}. Consider changing it to match the error response for remote service. Deserializer error: {1}", errorType, exc.Message);
        throw new Exception(explain, exc);
      }
    }
    #endregion

    private async Task<Exception> ReadError(HttpResponseMessage response) {
      switch (response.StatusCode) {
        case HttpStatusCode.BadRequest:
          if(BadRequestContentType == typeof(string))
            return await ReadErrorString(response);
          var serErrors = await Serializer.DeserializeAsync(BadRequestContentType, response.Content);
          if (BadRequestContentType == typeof(List<ClientFault>)) {
            var faults = (List<ClientFault>)serErrors.Object;
            return new ClientFaultException(faults);
          } else
            return new BadRequestException(serErrors.Object);
        default:
          if(ServerErrorContentType == typeof(string))
            return await ReadErrorString(response);
          //deserialize custom object
          try {
            var serErr = await Serializer.DeserializeAsync(ServerErrorContentType, response.Content);
            return new ApiException("Server error: " + serErr.Raw, response.StatusCode, serErr.Object);
          } catch(Exception ex) {
            var remoteErr = await response.Content.SafeReadContent();
            var msg = StringHelper.SafeFormat("Remote server error: {0}\r\n !!! Failed to deserialize response into error object, exc: {1}", remoteErr, ex.ToLogString()); 
            return new ApiException(msg, response.StatusCode, remoteErr);
          }
      }//switch 
    }

    private async Task<ApiException> ReadErrorString(HttpResponseMessage response) {
      var content = await response.Content.ReadAsStringAsync();
      string message, details; //if multiline, split
      SplitErrorMessage(content, out message, out details);
      return new ApiException(message, response.StatusCode, details);

    }

    private void SplitErrorMessage(string message, out string firstLine, out string others) {
      firstLine = message;
      others = null;
      if (string.IsNullOrWhiteSpace(message)) return;
      var nlPos = message.IndexOf('\n');
      if (nlPos < 0) return;
      firstLine = message.Substring(0, nlPos);
      others = message.Substring(nlPos + 1);
    }
  }
}
