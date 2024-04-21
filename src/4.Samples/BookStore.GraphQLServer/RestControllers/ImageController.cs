using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Vita.Entities;

namespace BookStore.GraphQL.Rest {

  // REST controller for serving images; GraphQL by default does not have facility to return binary data like pics
  [Route("api")]
  public class ImageController : Controller {

    // An example of returning binary stream from API controller. This end-point serves images for URLs directly
    // embedded into 'img' HTML tag. It is used for showing book cover pics
    [HttpGet, Route("images/{id}")]
    public Stream GetImage(Guid id) {
      var app = BooksEntityApp.Instance; 
      var session = app.OpenSession();
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
