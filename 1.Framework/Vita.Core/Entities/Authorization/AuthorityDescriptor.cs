using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Authorization {
  /// <summary>A container for Authority with Invalidated flag to allow global invalidation of Authority object when user roles change. </summary>
  public class AuthorityDescriptor {
    public readonly Authority Authority;
    public bool Invalidated;
    public AuthorityDescriptor(Authority authority) {
      Authority = authority; 
    }
  }
}
