using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Modules.Logging.Azure {

  private async Task<string> IngestAsync(Batch<T> outBatch) {
    var start = Stopwatch.GetTimestamp();
    var json = JsonConvert.SerializeObject(outBatch.Items);
    var dateString = AppTime.UtcNow.ToString("r");
    var jsonByteCount = Encoding.UTF8.GetBytes(json).Length; // json.Length;
    string signature = $"POST\n{jsonByteCount}\n{_jsonContentType}\nx-ms-date:{dateString}\n/api/logs";
    string signatureHash = HashSignature(signature);
    string authHeader = $"SharedKey {Config.CustomerId}:{signatureHash}";
    var request = new HttpRequestMessage(HttpMethod.Post, _targetUrl);
    request.Headers.Add("Accept", _jsonContentType);
    request.Headers.Add("Log-Type", Config.LogType);
    request.Headers.Add("Authorization", authHeader);
    request.Headers.Add("x-ms-date", dateString);
    request.Content = new StringContent(json, Encoding.UTF8);
    request.Content.Headers.ContentType = new MediaTypeHeaderValue(_jsonContentType);
    try {
      // global cancellation token will be signaled on domain unload event. 
      var response = await _httpClient.SendAsync(request, EnvironmentAdapter.Instance.CancellationToken);
      if (!response.IsSuccessStatusCode) {
        var responseContent = response.Content;
        string err = await responseContent.ReadAsStringAsync();
        outBatch.Exception = new Exception("Log analytics call error: " + err);
        return "Error";
      }

      return "OK";
    } catch (Exception ex) {
      outBatch.Exception = ex;
      return "Error";
    } finally {
      outBatch.Duration = Utility.GetDuration(start);
      outBatch.CreatedOn = AppTime.UtcNow;
    }
  }

  private string HashSignature(string signature) {
    var enc = new ASCIIEncoding();
    byte[] sigBytes = enc.GetBytes(signature);
    using (var hmacsha256 = new HMACSHA256(_sharedKeyBytes)) {
      byte[] hash = hmacsha256.ComputeHash(sigBytes);
      return Convert.ToBase64String(hash);
    }
  }
}
