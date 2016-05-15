using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.TextTemplates {

  public enum TemplateFormat {
    Text,
    Html,
    Wiki,
    Custom,
  }

  public enum TemplateType {
    Simple,
    T4, //to be implemented
  }

  [Entity, Unique("Name,OwnerId,Culture")]
  public interface ITextTemplate {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(100)]
    string Name {get;set;}
    TemplateFormat Format {get; set;}
    [Size(5), Nullable]
    string Culture { get; set; } // ex: EN-US
    [Size(20)]
    string Engine { get; set; }

    [Unlimited]
    string Template {get; set;}

    Guid? OwnerId {get; set;} //optional, UserId or party ID that owns it.   

  }
}
