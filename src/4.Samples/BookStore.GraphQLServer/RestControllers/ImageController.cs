using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Vita.Entities;
using Vita.Entities.Api;
using System.IO;
using Vita.Entities.Utilities;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Vita.Web;
using Microsoft.AspNetCore.Http;

namespace BookStore.GraphQLServer.Rest {

  // REST controller for serving images; GraphQL by default does not have facility to return binary data like pics
  [Route("api")]
  public class ImageController : BaseApiController {

    // An example of returning binary stream from API controller. This end-point serves images for URLs directly
    // embedded into 'img' HTML tag. It is used for showing book cover pics
    [HttpGet, Route("images/{id}")]
    public Stream GetImage(Guid id) {
      OpContext.WebContext.Flags |= WebCallFlags.NoLogResponseBody; //if you end up logging it for some reason, do not log body (image itself)
      var session = OpenSession();
      session.EnableLog(false); 
      var image = session.GetEntity<IImage>(id);
      if(image == null)
        return null;
      var rec = EntityHelper.GetRecord(image);
      // Looks like media type is set automatically (and correctly)
      // Context.WebContext.OutgoingHeaders.Add("Content-Type", image.MediaType);
      var stream = new MemoryStream(image.Data);
      return stream;
    }

  }
}
