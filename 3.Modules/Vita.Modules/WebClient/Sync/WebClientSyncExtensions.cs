using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Modules.WebClient.Sync {
  /// <summary>Defines sync methods as extensions to <c>WebApiClient</c> class. </summary>
  public static class WebClientSyncExtensions {

    public static TResult ExecuteGet<TResult>(this WebApiClient client, string url, params object[] args) {
      return AsyncHelper.RunSync(() => client.GetAsync<TResult>(url, args));
    }//method

    public static TResult ExecutePost<TContent, TResult>(this WebApiClient client, TContent content, string url, params object[] args) {
      return AsyncHelper.RunSync(() => client.PostAsync<TContent, TResult>(content, url, args));
    }

    public static TResult ExecutePut<TContent, TResult>(this WebApiClient client, TContent content, string url, params object[] args) {
      return AsyncHelper.RunSync(() => client.PutAsync<TContent, TResult>(content, url, args));
    }

    public static HttpStatusCode ExecuteDelete(this WebApiClient client, string url, params object[] args) {
      return AsyncHelper.RunSync(() => client.DeleteAsync(url, args));
    }

    public static byte[] ExecuteGetBinary(this WebApiClient client, string mediaType, string url, params object[] args) {
      return AsyncHelper.RunSync(() => client.GetBinaryAsync(mediaType, url, args));
    }

    public static string ExecuteGetString(this WebApiClient client, string mediaType, string url, params object[] args) {
      return AsyncHelper.RunSync(() => client.GetStringAsync(mediaType, url, args));
    }

  }//class
}
