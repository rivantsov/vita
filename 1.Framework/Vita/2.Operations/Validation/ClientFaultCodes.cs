using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities
{
  /// <summary>Standard code values used in ClientFault.Code field for some common cases.</summary>
  public static class ClientFaultCodes {
    public const string ValueTooLong = "ValueTooLong";
    public const string ValueMissing = "ValueMissing";
    public const string ValueOutOfRange = "ValueOutOfRange";
    public const string InvalidValue = "InvalidValue";
    public const string InvalidAction = "InvalidAction";
    public const string ObjectNotFound = "ObjectNotFound";
    public const string CircularEntityReference = "CircularEntityReference";
    public const string ContentMissing = "ContentMissing"; //missing HTTP message body when some object is expected
    public const string InvalidUrlParameter = "InvalidUrlParameter";
    public const string InvalidUrlOrMethod = "InvalidUrlOrMethod";
    public const string ConcurrentUpdate = "ConcurrentUpdate";
    public const string BadContent = "BadContent"; // failure to deserialize content
    public const string AuthenticationRequired = "AuthenticationRequired";
  }

}
