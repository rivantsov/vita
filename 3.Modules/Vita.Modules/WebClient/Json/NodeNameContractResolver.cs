using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Vita.Entities.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Vita.Modules.WebClient.Json {
  /// <summary>Handles NodeAttribute that specifies Json node name.</summary>
  /// <remarks>Node attribute is equivalent to JsonProperty attribute in Newtonsoft library, but it is serializer-independent, so it can be used 
  /// for other Json or Xml serializers. </remarks>
  public class NodeNameContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver {
    public readonly bool ChangeToCamelCase;

    public NodeNameContractResolver(ClientOptions options) {
      ChangeToCamelCase = options.IsSet(ClientOptions.CamelCaseNames);
    }
    // Note: this method is called just once for a given property; Newtonsoft serializer caches metadata information,
    // so the result is cached and reused. We are not concerned with efficiency here
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
      var property = base.CreateProperty(member, memberSerialization);
      var nodeAttr = member.GetCustomAttribute<NodeAttribute>(inherit: true);
      if (nodeAttr != null)
        property.PropertyName = nodeAttr.Name; 
      else if (ChangeToCamelCase)
        property.PropertyName = property.PropertyName.ToCamelCase();
      return property;
    }

  }// class
}
