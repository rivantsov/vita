using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities {

  public static class ValidationExtensions {

    public static ClientFault ValidateEntity(this IEntitySession session, object entity, bool condition, string faultCode,
                                         string propertyName, object invalidValue, string message, params object[] args) {
      return session.Context.ValidateEntity(entity, condition, faultCode, propertyName, invalidValue, message, args);
    }

    public static ClientFault ValidateEntity(this OperationContext context, object entity, bool condition, string faultCode,
                                         string propertyName, object invalidValue, string message, params object[] args) {
      if(condition)
        return null;
      var rec = EntityHelper.GetRecord(entity);
      var entRef = rec.GetEntityRef();
      var fault = new ClientFault() { Code = faultCode, PropertyName = propertyName, 
        Tag = propertyName, Message = Util.SafeFormat(message, args), EntityRef = entRef };
      if(invalidValue != null)
        fault.Parameters["InvalidValue"] = invalidValue.ToString();
      rec.AddValidationFault(fault);
      return fault;
    }

    public static ClientFault ValidateTrue(this OperationContext context, bool condition, string faultCode,
                                         string valueName, object invalidValue, string message, params object[] args) {
      if(condition)
        return null;
      var msg = Util.SafeFormat(message, args);
      var fault = new ClientFault() { Code = faultCode, Tag = valueName, Message = msg };
      if(invalidValue != null)
        fault.Parameters["InvalidValue"] = invalidValue.ToString();
      context.AddClientFault(fault);
      return fault;
    }

    public static ClientFault ValidateNotNull(this OperationContext context, object value,
                                         string valueName, string message, params object[] args) {
      return context.ValidateTrue(value != null, ClientFaultCodes.ObjectNotFound, valueName, null, message, args);
    }

    public static ClientFault ValidateNotEmpty(this OperationContext context, string value,
                                         string valueName, string message, params object[] args) {
      return context.ValidateTrue(!string.IsNullOrWhiteSpace(value), ClientFaultCodes.ValueMissing, valueName, value, message, args);
    }

    public static ClientFault ValidateMaxLength(this OperationContext context, string value, int maxLength,
                                         string valueName, string message, params object[] args) {
      return context.ValidateTrue(value == null || value.Length <= maxLength, ClientFaultCodes.ValueTooLong, valueName, value, message, args);
    }

    public static ClientFault ValidateRange<TValue>(this OperationContext context, TValue value, TValue min, TValue max,
                                                   string valueName, string message, params object[] args)
                                                   where TValue : struct, IComparable {
      var cond = value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
      return context.ValidateTrue(cond, ClientFaultCodes.ValueOutOfRange, valueName, value, message, args);
    }

    public static void ThrowValidation(this OperationContext context) {
      context.ThrowValidation();
    }

    public static void ThrowIfNull(this OperationContext context, object value, string faultCode, string valueName, string message, params object[] args) {
      ThrowIf(context, value == null, faultCode, valueName, message, args);
    }

    public static void ThrowIfEmpty(this OperationContext context, string value, string faultCode, string valueName, string message, params object[] args) {
      ThrowIf(context, string.IsNullOrWhiteSpace(value), faultCode, valueName, message, args);
    }


    public static void ThrowIf(this OperationContext context, bool condition, string faultCode, string valueName, string message, params object[] args) {
      if(!condition)
        return;
      var msg = Util.SafeFormat(message, args);
      var fault = new ClientFault() { Code = faultCode, Tag = valueName, Message = msg };
      context.AddClientFault(fault);
      context.ThrowValidation();
    }

  }//class

}
