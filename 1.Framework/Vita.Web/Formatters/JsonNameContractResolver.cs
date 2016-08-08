using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Vita.Common;
using Vita.Entities.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Vita.Web {
  // Note: this class is identical to contract resolver in Vita.Modules.WebClient namespace. To avoid extra referencing the assembly 
  // (which seems inapropriate) we duplicate this simple class here under different name in 2 assemblies

  /// <summary>Handles NodeAttribute that specifies Json node name. Provides conversion of names from model to Json and back according to NameMapping setting. </summary>
  /// <remarks>Node attribute is equivalent to JsonProperty attribute in Newtonsoft library, but it is serializer-independent, so it can be used 
  /// for other Json or Xml serializers. </remarks>
  public class JsonNameContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver {

    public readonly ApiNameMapping NameMapping; 

    public JsonNameContractResolver(ApiNameMapping nameMapping) {
      NameMapping = nameMapping; 
    }

    // Note: this method is called just once for a given property; Newtonsoft serializer caches metadata information,
    // so the result is cached and reused. We are not concerned with efficiency here
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
      var property = base.CreateProperty(member, memberSerialization);
      var nodeAttr = member.GetCustomAttribute<NodeAttribute>(inherit: true);
      if (nodeAttr != null) {
        property.PropertyName = nodeAttr.Name;
        return property; 
      }
      switch(NameMapping) {
        case ApiNameMapping.Default: break; //nothing to do
        case ApiNameMapping.CamelCase:
          property.PropertyName = property.PropertyName.ToCamelCase();
          break;
        case ApiNameMapping.UnderscoreAllLower:
          property.PropertyName = property.PropertyName.ToUnderscoreAllLower();
          break; 
      }
      return property;
    }

  }// class
}
