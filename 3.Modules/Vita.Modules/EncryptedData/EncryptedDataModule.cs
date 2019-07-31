using System;
using System.Collections.Generic;
using System.Security.Cryptography;

using Vita.Entities;

namespace Vita.Modules.EncryptedData {

  public class EncryptedDataModule : EntityModule, IEncryptionService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0"); 

    public static string DefaultSeed = "C155A981-BD2D-11E4-8939-FAEE54130FBE"; //just random guid
    private Dictionary<string, EncryptionChannel> _channels = new Dictionary<string, EncryptionChannel>(StringComparer.OrdinalIgnoreCase);
    private EncryptionChannel _defaultChannel; 

    public EncryptedDataModule(EntityArea area): base(area, "EncryptedData", version: CurrentVersion) {
      RegisterEntities(typeof(IEncryptedData));
      App.RegisterService<IEncryptionService>(this); 
    }


    #region IEncryptionService methods
  
    public void AddChannel(byte[] cryptoKey, SymmetricAlgorithm algorithm = null, string channelName = null) {
      algorithm = algorithm ?? SymmetricAlgorithm.Create();
      var channel = new EncryptionChannel(channelName, cryptoKey, algorithm);
      if(channelName == null)
        _defaultChannel = channel;
      else 
        _channels[channelName] = channel;
    }

    public byte[] Encrypt(byte[] data, string seed, string channelName = null) {
      var ch = GetChannel(channelName);
      return ch.Encrypt(data, seed); 
    }

    public byte[] Decrypt(byte[] data, string seed, string channelName = null) {
      var ch = GetChannel(channelName);
      return ch.Decrypt(data, seed); 
    }

    public bool IsRegistered(string channelName) {
      if(string.IsNullOrWhiteSpace(channelName))
        return _defaultChannel != null;
      else
        return _channels.ContainsKey(channelName); 
    }
    #endregion

    private EncryptionChannel GetChannel(string name) {
      if(name == null) {
        Util.Check(_defaultChannel != null, "Default encryption channel is not configured.");
        return _defaultChannel; 
      }
      EncryptionChannel ch = null;
      if(_channels.TryGetValue(name, out ch))
        return ch;
      Util.Throw("Encryption channel {0} not configured.", name);
      return null; 
    }
  }//class

}//ns
