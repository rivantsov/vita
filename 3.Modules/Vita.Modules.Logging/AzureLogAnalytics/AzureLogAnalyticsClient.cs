using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging.Azure {

  // DRAFT, to be completed

  public class AzureLogAnalyticsClient {
    const string JsonContentType = "application/json";
    string _customerId;
    byte[] _keyBytes;
    string _targetUrl;
    static HttpClient _httpClient;

    private async Task SendAsync(IList<LogEntry> items, string tableName)  {
      var cancelTokenSource = new CancellationTokenSource();
      var json = JsonConvert.SerializeObject(items);
      var dateString = DateTime.UtcNow.ToString("r");
      var jsonByteCount = Encoding.UTF8.GetBytes(json).Length; // json.Length;
      string signature = $"POST\n{jsonByteCount}\n{JsonContentType}\nx-ms-date:{dateString}\n/api/logs";
      string signatureHash = HashSignature(signature);
      string authHeader = $"SharedKey {_customerId}:{signatureHash}";
      var request = new HttpRequestMessage(HttpMethod.Post, _targetUrl);
      request.Headers.Add("Accept", JsonContentType);
      request.Headers.Add("Log-Type", tableName);
      request.Headers.Add("Authorization", authHeader);
      request.Headers.Add("x-ms-date", dateString);
      request.Content = new StringContent(json, Encoding.UTF8);
      request.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);
      var response = await _httpClient.SendAsync(request, cancelTokenSource.Token);
      if(!response.IsSuccessStatusCode)
      {
        var responseContent = response.Content;
        string err = await responseContent.ReadAsStringAsync();
        throw new Exception("Log analytics call error: " + err);
      }
    }

    private string HashSignature(string signature)
    {
      var enc = new ASCIIEncoding();
      byte[] sigBytes = enc.GetBytes(signature);
      using(var hmacsha256 = new HMACSHA256(_keyBytes))
      {
        byte[] hash = hmacsha256.ComputeHash(sigBytes);
        return Convert.ToBase64String(hash);
      }
    }

  } //class
}
