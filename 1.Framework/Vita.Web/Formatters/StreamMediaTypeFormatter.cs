using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Web {
  /// <summary>Formatter for binary data. Makes it possible to return/receive binary data as stream
  /// in Web Api controllers.
  /// </summary>
  /// <remarks>The formatter handles request/response bodies represented by Stream objects. 
  /// The common use is for returning images from Web Api methods. 
  /// To return a jpeg image from an API method, return a binary stream object containing image bytes 
  /// and set the content-type header (through Context.WebContext.OutgoingHeaders) to image/jpeg. 
  /// </remarks>
  public class StreamMediaTypeFormatter : BufferedMediaTypeFormatter {
    MediaTypeHeaderValue _defaultMediaType; 

    public StreamMediaTypeFormatter(params string[] mediaTypes) {
      if (mediaTypes == null || mediaTypes.Length == 0)
        mediaTypes = new string[] { "image/jpeg", "image/webp" };
      foreach (var type in mediaTypes)
        SupportedMediaTypes.Add(new System.Net.Http.Headers.MediaTypeHeaderValue(type));
      _defaultMediaType = new MediaTypeHeaderValue(mediaTypes[0]);
    }

    public override bool CanReadType(Type type) {
      if (typeof(Stream).IsAssignableFrom(type))
        return true;
      return false;
    }

    public override bool CanWriteType(Type type) {
      if (typeof(Stream).IsAssignableFrom(type))
        return true;
      return false;
    }

    public override object ReadFromStream(Type type, Stream readStream, HttpContent content, IFormatterLogger formatterLogger) {
      return base.ReadFromStream(type, readStream, content, formatterLogger);
    }

    public override void WriteToStream(Type type, object value, Stream writeStream, HttpContent content) {
      if (value != null) {
        var stream = (Stream)value;
        stream.CopyTo(writeStream);
      }
    }

    public override void SetDefaultContentHeaders(Type type, HttpContentHeaders headers, MediaTypeHeaderValue mediaType) {
      base.SetDefaultContentHeaders(type, headers, _defaultMediaType);
    }

  }//class

}//ns
