using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Logging {
  [Entity, DoNotTrack]
  public interface IWebClientLog : ILogEntityBase {
    [Size(Sizes.Name), Nullable]
    string ClientName { get; set; }
    int Duration { get; set; }
    [Size(10)]
    string HttpMethod { get; set; }
    [Size(200)]
    string Server { get; set; } //protocol, domain address and port
    [Unlimited, Nullable]
    string PathQuery { get; set; }
    [Unlimited, Nullable]
    string CallTemplate { get; set; } //template used in a call, with placeholders like {0}
    [Nullable, Unlimited]
    string RequestHeaders { get; set; }
    [Unlimited, Nullable]
    string RequestBody { get; set; }
    long RequestSize { get; set; }

    //Response
    HttpStatusCode? ResponseHttpStatus { get; set; }
    [Nullable, Unlimited]
    string ResponseHeaders { get; set; }
    [Unlimited, Nullable]
    string ResponseBody { get; set; }
    long ResponseSize { get; set; }

    [Nullable, Unlimited]
    string Error { get; set; }
    Guid? ErrorLogId { get; set; }

    // hashes with indexes, used for fast search
    [HashFor("Server"), Index]
    int ServerHash { get; }
    [HashFor("CallTemplate"), Index]
    int CallTemplateHash { get; }

  }
}
