using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model.Construction {

  public class CustomAttributeHandler : IAttributeHandler {
    public Type AttributeType;
    public CustomAttributeHandler(Type attributeType) {
      AttributeType = attributeType; 
    }

    public AttributeApplyOrder ApplyOrder { get { return AttributeApplyOrder.Last; } }
    public virtual void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {}
    public virtual void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) { }
    public virtual CustomAttributeBuilder Clone(Attribute attribute) {
      return null; 
    }

    public static IDictionary<Type, CustomAttributeHandler> GetDefaultHandlers() {
      var result = new Dictionary<Type, CustomAttributeHandler>();
      result.Add(typeof(DescriptionAttribute), new DescriptionAttributeHandler());
      result.Add(typeof(BrowsableAttribute), new BrowsableAttributeHandler());
      result.Add(typeof(DisplayNameAttribute), new DisplayNameAttributeHandler());
      result.Add(typeof(CategoryAttribute), new CategoryAttributeHandler());
      return result; 
    }
  }

  public class DescriptionAttributeHandler : CustomAttributeHandler {
    
    public DescriptionAttributeHandler() : base(typeof(DescriptionAttribute)) { }

    public override void Apply(AttributeContext context, Attribute attribute, EntityInfo entity) {
      var descAttr = (DescriptionAttribute)attribute;
      entity.Description = descAttr.Description;
    }

    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      var descAttr = (DescriptionAttribute)attribute;
      member.Description = descAttr.Description;
    }
    public override CustomAttributeBuilder Clone(Attribute attribute) {
      var descAttr = (DescriptionAttribute)attribute;
      return new CustomAttributeBuilder(typeof(DescriptionAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { descAttr.Description });
    }
  }

  public class BrowsableAttributeHandler : CustomAttributeHandler {
    public BrowsableAttributeHandler() : base(typeof(BrowsableAttribute)) { }
    public override CustomAttributeBuilder Clone(Attribute attribute) {
      var instance = (BrowsableAttribute)attribute;
      return new CustomAttributeBuilder(typeof(BrowsableAttribute).GetConstructor(new Type[] { typeof(bool) }), new object[] { instance.Browsable });
    }
  }
  public class CategoryAttributeHandler : CustomAttributeHandler {
    public CategoryAttributeHandler() : base(typeof(CategoryAttribute)) { }
    public override CustomAttributeBuilder Clone(Attribute attribute) {
      var instance = (CategoryAttribute)attribute;
      return new CustomAttributeBuilder(typeof(CategoryAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { instance.Category });
    }
  }
  public class DisplayNameAttributeHandler : CustomAttributeHandler {
    public DisplayNameAttributeHandler() : base(typeof(DisplayNameAttribute)) { }
    public override CustomAttributeBuilder Clone(Attribute attribute) {
      var instance = (DisplayNameAttribute)attribute;
      return new CustomAttributeBuilder(typeof(DisplayNameAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { instance.DisplayName });
    }
    public override void Apply(AttributeContext context, Attribute attribute, EntityMemberInfo member) {
      base.Apply(context, attribute, member);
      var da = (DisplayNameAttribute)attribute;
      member.DisplayName = da.DisplayName; 
    }
    //DisplayName attr cannot be put on interfaces, so we do not have Apply override at entity level
  }


}//ns
