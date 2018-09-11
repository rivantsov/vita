using System;

namespace Vita.Entities.Api {
  // The attributes to use on SlimApi controllers; work similar to Web API attributes like HttpGet/Route, etc. These attributes do not  reference Web API packages, 
  // so API controllers can be built directly in the core entity assemblies. As an example, Login module in Vita.Modules project defines all API controllers 
  // that are needed for implementing login functionality in Web applications. 

  [AttributeUsage(AttributeTargets.Class)]
  public class ApiRoutePrefixAttribute : Attribute {
    public string Prefix;
    public ApiRoutePrefixAttribute(string prefix) {
      Prefix = prefix;
    }
  }//class

  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
  public class ApiRouteAttribute : Attribute {
    public string Template;
    public ApiRouteAttribute(string template = null) {
      Template = template;
    }
  }//class

  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
  public class ApiMethodAttribute : Attribute {
    public readonly string Method;
    public ApiMethodAttribute(string method = "GET") {
      Method = method.ToUpperInvariant(); 
    }
    //this constructor is only to satisfy CLS-compliance
    protected ApiMethodAttribute() { }
  }

  [AttributeUsage(AttributeTargets.Method)]
  public class ApiGetAttribute : ApiMethodAttribute {
    public ApiGetAttribute() : base("GET") { }
  }

  [AttributeUsage(AttributeTargets.Method)]
  public class ApiPostAttribute : ApiMethodAttribute {
    public ApiPostAttribute() : base("POST") { }
  }

  [AttributeUsage(AttributeTargets.Method)]
  public class ApiPutAttribute : ApiMethodAttribute {
    public ApiPutAttribute() : base("PUT") { }
  }

  [AttributeUsage(AttributeTargets.Method)]
  public class ApiDeleteAttribute : ApiMethodAttribute {
    public ApiDeleteAttribute() : base("DELETE") { }
  }

  /// <summary>Restricts a controller or method to authenticated users only. </summary>
  /// <remarks>Controller-level restriction overrides method-level restriction. 
  /// Controller-level restriction can be overwritten during controller registration.</remarks>
  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
  public class LoggedInOnlyAttribute : Attribute {}
    
  /// <summary>Indicates that a controller or method is secured by authorization rules.</summary>
  /// <remarks>The controller is available only if access is explicitly granted as part of user Roles/Permissions setup. </remarks>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
  public class SecuredAttribute : Attribute {
    public SecuredAttribute() { }
  }

  /// <summary>Equivalent to FromUri attribute in Web Api.</summary>
  [AttributeUsage(AttributeTargets.Parameter)]
  public class FromUrlAttribute : Attribute {}

  /// <summary>Name of json element, equivalent to JsonProperty.</summary>
  /// <remarks>The attribute allows to define model objects without referencing NewtonSoft assembly.
  /// Handled by custom NodeNameContractResolver. </remarks>
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
  public class NodeAttribute : Attribute {
    public string Name;
    public NodeAttribute(string name) {
      Name = name;
    }
  }

  [AttributeUsage(AttributeTargets.Class)]
  public class ApiGroupAttribute : Attribute {
    public readonly string Group;
    public ApiGroupAttribute(string group) {
      Group = group; 
    }
  }


}//ns
