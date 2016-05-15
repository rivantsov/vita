using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using Vita.Entities;

namespace Vita.Web.SlimApi {


  internal class OperationContextBinderAttribute : ModelBinderAttribute {
    public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
      return new OperationContextBinding(parameter);
    }
  }

  internal class OperationContextBinding : HttpParameterBinding {
    //Copied from TaskHelpers.cs in Web api
    private struct AsyncVoid { }
    private static readonly Task _defaultCompleted = Task.FromResult<AsyncVoid>(default(AsyncVoid));

    public OperationContextBinding(HttpParameterDescriptor descriptor) : base(descriptor) { }
    public override Task ExecuteBindingAsync(System.Web.Http.Metadata.ModelMetadataProvider metadataProvider, HttpActionContext actionContext, System.Threading.CancellationToken cancellationToken) {
      object value = null; 
      var webContext = WebHelper.GetWebCallContext(actionContext.Request);
      if(webContext != null)
        value = webContext.OperationContext; 
      actionContext.ActionArguments[base.Descriptor.ParameterName] = value;
      return _defaultCompleted;
    }
  }


}
