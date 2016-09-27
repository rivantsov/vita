using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Modules.WebClient;
using Vita.Modules.WebClient.Sync;

namespace Vita.Samples.BookStore.SampleData.Import {

  /// <summary>GoogleBooks API client. A sample of strongly typed client class for a particular API. </summary>
  public class GoogleBooksApiClient {
    const string GoogleBooksUrl = "https://www.googleapis.com/books/v1";
    public WebApiClient ApiClient;

    public GoogleBooksApiClient(OperationContext context) {
      ApiClient = new WebApiClient(context, GoogleBooksUrl, ClientOptions.Default, nameMapping: Entities.Web.ApiNameMapping.CamelCase, 
          badRequestContentType: typeof(GoogleBadRequestResponse));
    }

    public VolumeSet GetVolumes(string keywords, int skip = 0, int take = 40) {
      Util.Check(take <= 40, "Max value for 'take' parameter is 40 (maxResults parameter in GoogleApi).");
      var volumeSet = ApiClient.ExecuteGet<VolumeSet>("volumes?q={0}&startIndex={1}&maxResults={2}&projection=lite", keywords, skip, take);
      return volumeSet; 
    }

    public byte[] GetImage(string url) {
      var bytes = ApiClient.ExecuteGetBinary("image/jpeg", url);
      return bytes; 
    }

  }//class
}//ns
