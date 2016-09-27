using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Collections.ObjectModel;
using Vita.Entities;
using Vita.Common;
using Vita.Entities.Web;

namespace Vita.Modules.WebClient.Json {

  public class JsonContentSerializer : IContentSerializer {
    public const string JsonMediaType = "application/json";
    // Some APIs follow this draft for errors in REST APIs: https://tools.ietf.org/html/draft-nottingham-http-problem-07
    // So returned error is JSon but content type is problem+json
    public const string JsonErrorMediaType = "application/problem+json";
    public readonly JsonSerializer JsonSerializer;
    public readonly JsonSerializerSettings SerializerSettings;
    int _maxDepth = 50;
    public bool Indent = true;
    public Collection<Encoding> SupportedEncodings = new Collection<Encoding>();

    public JsonContentSerializer(ClientOptions clientOptions, ApiNameMapping nameMapping = ApiNameMapping.Default, JsonSerializerSettings settings = null) {
      if (settings == null) {
        settings = new JsonSerializerSettings();
        settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter()); //serialize enum as names, not numbers
      }
      SerializerSettings = settings;
      if (SerializerSettings.ContractResolver == null) {
        SerializerSettings.ContractResolver = new ClientJsonNameContractResolver(nameMapping); //to process NodeAttribute attribute
      }
      JsonSerializer = JsonSerializer.Create(SerializerSettings);
      SupportedEncodings.Add(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
      SupportedEncodings.Add(new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true));
      _mediaTypes.Add(JsonMediaType);
      _mediaTypes.Add(JsonErrorMediaType);
    }

    public IList<string> MediaTypes {
      get { return _mediaTypes; }
    } IList<string> _mediaTypes = new List<string>(); 

    public async Task<SerializedContent> DeserializeAsync(Type type, HttpContent content) {
      HttpContentHeaders headers = content == null ? null : content.Headers;
      // If content length is 0 then return default value for this type
      if (headers != null) {
        if (headers.ContentLength == 0)
          return new SerializedContent() { Object = ReflectionHelper.GetDefaultValue(type), Content = content };
        var mediaType = headers.ContentType.MediaType;
        Util.Check(_mediaTypes.Contains(mediaType), "JsonContentSerializer: cannot deserialize response body with content type {0}.", mediaType);
      }
      //Get stream
      var stream = await content.ReadAsStreamAsync();
      //read raw data
      var reader = new StreamReader(stream);
      var raw = reader.ReadToEnd();
      //deserialize
      stream.Position = 0; 
      Encoding effectiveEncoding = SelectCharacterEncoding(headers);
      using (JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(stream, effectiveEncoding)) 
           { CloseInput = false, MaxDepth = _maxDepth }) {
        var obj = JsonSerializer.Deserialize(jsonTextReader, type);
        return new SerializedContent() { Object = obj, Content = content, Raw = raw };
      }
    }

    public async Task<SerializedContent> SerializeAsync(object value) {
      var stream = new MemoryStream();
      var effectiveEncoding = SupportedEncodings[0];
      if (value != null) {
        using (JsonTextWriter jsonTextWriter = new JsonTextWriter(new StreamWriter(stream, effectiveEncoding)) { CloseOutput = false }) {
          if (Indent)
            jsonTextWriter.Formatting = Newtonsoft.Json.Formatting.Indented;
          JsonSerializer.Serialize(jsonTextWriter, value);
          jsonTextWriter.Flush();
        }
      }
      //Get raw content
      stream.Position = 0;
      var reader = new StreamReader(stream);
      var raw = reader.ReadToEnd();
      //create httpContent
      var content = new StringContent(raw);
      content.Headers.ContentType = new MediaTypeHeaderValue(JsonMediaType);
      content.Headers.ContentType.CharSet = effectiveEncoding.WebName;
      var serCont = new SerializedContent() { Object = value, Raw = raw, Content = content };
      return await Task.FromResult(serCont); 
    }


    public Encoding SelectCharacterEncoding(HttpContentHeaders contentHeaders) {
      if (contentHeaders != null && contentHeaders.ContentType != null) {
        // Find encoding based on content type charset parameter
        var charset = contentHeaders.ContentType.CharSet;
        if (!String.IsNullOrWhiteSpace(charset)) {
          var encoding =
              SupportedEncodings.FirstOrDefault(
                  enc => charset.Equals(enc.WebName, StringComparison.OrdinalIgnoreCase));
          if (encoding != null)
            return encoding;
        }
      }
      if (SupportedEncodings.Count > 0) {
        // We didn't find a character encoding match based on the content headers.
        // Instead we try getting the default character encoding.
        return SupportedEncodings[0];
      }
      Util.Throw("JSonContentSerializer: SupportedEncodings list is empty.");
      return null; //never happens
    }

  }//class
}
