using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Logging;


namespace Vita.Entities.Model.Construction {

  // General interface for attribute handlers. There are 2 kinds of handlers:
  // 1. Self-handling attributes - most model attributes (like IndexAttribute) implement IAttributeHandler, so they handle themselves
  // 2. CustomAttributeHandlers - for standard .NET attributes like DescriptionAttribute. The handlers for attributes should be registered in 
  //    EntityModelSetup.AttributeHandlers dictionary. The system creates a few custom handlers at startup automatically.
  public interface IAttributeHandler {
    AttributeApplyOrder ApplyOrder { get; }
    void Apply(AttributeContext context, Attribute attribute, EntityInfo entity);
    void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member);
    // This method is used to clone (create a copy) attributes from entity interfaces to IL-generated entity classes.
    CustomAttributeBuilder Clone(Attribute attribute);
  }

  
  // Attributes can specify it's relative apply order
  public enum AttributeApplyOrder {
    System = 0, // Framework uses the attribute following its own logic
    First = 10,
    Middle = 20,
    Last = 30,
    Default = Middle,
  }

  // a container for passing context information to IAttributeHandler
  public class AttributeContext {
    public readonly EntityModel Model;
    public MemoryLog Log;
    public AttributeContext(EntityModel model, MemoryLog activationLog) {
      Model = model; 
      Log = activationLog;
    }
  }



}
