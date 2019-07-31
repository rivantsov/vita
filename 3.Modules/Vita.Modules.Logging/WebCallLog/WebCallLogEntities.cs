using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Web;

namespace Vita.Modules.Logging {

  [Entity, DoNotTrack]
  public interface IWebCallLog : ILogEntityBase {
    int  Duration { get; set; }
    
    [Nullable, Unlimited]
    string Url{ get; set; }
    [Nullable, Size(250)]
    string UrlTemplate { get; set; }
    [Nullable, Size(250)]
    string UrlReferrer { get; set; }
    [Nullable, Size(Sizes.IPv6Address)] //50
    string IPAddress { get; set; }
    [Nullable]
    string ControllerName { get; set; }
    [Nullable]
    string MethodName { get; set; }
    
    //Request
    [Size(10), Nullable]
    string HttpMethod{ get; set; }
    WebCallFlags Flags { get; set; }
    [Unlimited, Nullable]
    string CustomTags { get; set; }
    [Nullable, Unlimited]
    string RequestHeaders { get; set; }
    [Nullable, Unlimited]
    string RequestBody { get; set; }
    long? RequestSize{ get; set; }
    int RequestObjectCount{ get; set; } //arbitrary, app-specific count of 'important' objects
    
    //Response
    HttpStatusCode HttpStatus{ get; set; }
    [Nullable, Unlimited]
    string ResponseHeaders { get; set; }
    [Nullable, Unlimited]
    string ResponseBody { get; set; }
    long? ResponseSize{ get; set; }
    int ResponseObjectCount{ get; set; } //arbitrary, app-specific count of 'important' objects

    //log and exceptions
    [Nullable, Unlimited]
    string LocalLog { get; set; }
    [Nullable, Unlimited]
    string Error { get; set; }
    [Nullable, Unlimited]
    string ErrorDetails { get; set; }

    Guid? ErrorLogId{ get; set; } 
  }

}
