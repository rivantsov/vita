using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common; 
using Vita.Entities;

namespace Vita.Modules.EncryptedData {
  public static class EncryptedDataExtensions {


    public static IEncryptedData NewOrUpdate(this IEntitySession session, IEncryptedData data, byte[] value, string channelName = null) {
      if(data == null)
        return NewEncryptedData(session, value, channelName);
      //update
      var encrService = GetService(session);
      data.Data = encrService.Encrypt(value, data.Id.ToString(), channelName);
      return data; 
    }

    public static IEncryptedData NewOrUpdate(this IEntitySession session, IEncryptedData data, string value, string channelName = null) {
      var bytes = Encoding.UTF8.GetBytes(value);
      return NewOrUpdate(session, data, bytes, channelName); 
    }

    // implementing NewEncryptedData as non-extension method, to discourage use. Use NewOrUpdate instead.
    public static IEncryptedData NewEncryptedData(IEntitySession session, string value, string channelName = null) {
      var bytes = Encoding.UTF8.GetBytes(value);
      return NewEncryptedData(session, bytes, channelName);
    }

    public static IEncryptedData NewEncryptedData(IEntitySession session, byte[] data, string channelName = null) {
      var encrService = GetService(session);
      var ent = session.NewEntity<IEncryptedData>();
      ent.Data = encrService.Encrypt(data, ent.Id.ToString(), channelName);
      return ent;
    }

    public static byte[] Decrypt(this IEncryptedData data, string channelName = null) {
      var session = EntityHelper.GetSession(data);
      var encrService = GetService(session);
      var bytes = encrService.Decrypt(data.Data, data.Id.ToString(), channelName);
      return bytes; 
    }

    public static string DecryptString(this IEncryptedData data, string channelName = null) {
      var bytes = Decrypt(data, channelName);
      var result = Encoding.UTF8.GetString(bytes);
      return result; 
    }

    //Utility
    private static IEncryptionService GetService(IEntitySession session) {
      var encrService = session.Context.App.GetService<IEncryptionService>();
      Util.Check(encrService != null, "Failed to retrieve IEncryptionService.");
      return encrService; 
    }

  }//class
}//ns
