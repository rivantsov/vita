using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.EncryptedData {

  [Entity]
  public interface IEncryptedData {
    [Auto, PrimaryKey]
    Guid Id { get; }

    [Unlimited]
    byte[] Data { get; set; }

  }

}
