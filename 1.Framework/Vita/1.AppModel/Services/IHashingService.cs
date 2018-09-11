using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Services {
  public interface IHashingService {
    int ComputeHash(string value);
    string ComputeMd5(string value);
  }
}
