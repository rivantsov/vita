using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.EncryptedData {

  /// <summary>Provides encryption services. </summary>
  public interface IEncryptionService {
    //there may be 1 or more named channels for encryption, with different algorithm and crypto key
    void AddChannel(byte[] cryptoKey, SymmetricAlgorithm algorithm = null, string channelName = null);
    bool IsRegistered(string channelName); 
    //Seed is random string used to create initialization vector for encryptor. Must be the same when you encrypt or decrypt the same value. 
    byte[] Encrypt(byte[] data, string seed, string channelName = null);
    byte[] Decrypt(byte[] data, string seed, string channelName = null);
  }

}
