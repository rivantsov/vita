using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Vita.Entities;


namespace Vita.Web {
  /// <summary>Exception indicating that input model state is invalid - typically a failure to deserialize the request body. </summary>
  /// <remarks>
  /// Most often indicates deserialization failure of input Json/Xml string in the media type formatter. 
  /// The CheckModelState attribute throws this exception.  
  /// </remarks>
  public class ModelStateException : OperationAbortException {
    public const string ReasonBadRequestBody = "BadRequestBody";
    public const string KeyModelStateErrors = "ModelStateErrors";

    
    public string ModelStateErrors;

    public ModelStateException(System.Web.Http.ModelBinding.ModelStateDictionary modelState, string message = "Bad request body.")
             : base(message, ReasonBadRequestBody) {
      base.LogAsError = true;
      ModelStateErrors = modelState.AsString(); //WebHelper method
      base.Data[KeyModelStateErrors] = this.ModelStateErrors; //to automatically include in ToLogString() representation
    }
  }

}
