using System;

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
