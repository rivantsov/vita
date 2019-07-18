using System;
using System.Collections.Generic;
using System.Text;

using Vita.Entities;
using Vita.Entities.Api; 

namespace Vita.Samples.BookStore.Api {
  public class BooksControllerBase : SlimApiController {

    protected IEntitySession OpenSession() {
      return Context.OpenSession();
    }

  }
}
