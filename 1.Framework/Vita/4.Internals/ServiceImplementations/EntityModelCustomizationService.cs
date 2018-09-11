using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Utilities;

using Vita.Entities.Model;
using Vita.Entities.Model.Construction;

namespace Vita.Entities.Services.Implementations {

  internal class EntityModelCustomizationService : IEntityModelCustomizationService {
    EntityApp _app;
    Sizes.SizeTable SizeTable = Sizes.GetDefaultSizes();
    internal bool Closed; 

    public EntityModelCustomizationService(EntityApp app) {
      _app = app; 
    }
    private void CheckNotClosed() {
      Util.Check(!Closed, "Model customization service may not be used at this stage, model construction already started.");
    }

    #region added members
    internal class AddedMemberInfo {
      public Type EntityType;
      public string Name;
      public Type DataType;
      public int Size;
      public bool Nullable;
      public Attribute[] Attributes; 
    }

    internal List<AddedMemberInfo> AddedMembers = new List<AddedMemberInfo>(); 

    public void AddMember(Type entityType, string name, Type memberType, int size = 0, bool nullable = false, Attribute[] attributes = null) {
      CheckNotClosed(); 
      Util.CheckParam(entityType, nameof(entityType));
      Util.Check(entityType.IsInterface, "Invalid entity type {0}, must be an interface.", entityType);
      Util.CheckParamNotEmpty(name, nameof(name));
      Util.CheckParam(memberType, nameof(memberType));
      AddedMembers.Add(new AddedMemberInfo() { EntityType = entityType, Name = name, DataType = memberType, Size = size, Nullable = nullable, Attributes = attributes });
    }
    #endregion

    #region added indexes
    internal class AddedIndexInfo {
      public Type EntityType;
      public IndexAttribute IndexAttribute; 
    }

    internal List<AddedIndexInfo> AddedIndexes = new List<AddedIndexInfo>();

    public void AddIndex(Type entityType, IndexAttribute index) {
      CheckNotClosed();
      Util.CheckParam(entityType, nameof(entityType));
      Util.Check(entityType.IsInterface, "Invalid entity type {0}, must be an interface.", entityType);
      Util.CheckParam(index, nameof(index));
      AddedIndexes.Add(new AddedIndexInfo() { EntityType = entityType, IndexAttribute = index });
    }
    #endregion 

    #region Entity replacements
    //Used in entity replacement setup
    public class EntityReplacementInfo {
      public Type ReplacedType;
      public Type NewType;
    }
    internal readonly List<EntityReplacementInfo> Replacements = new List<EntityReplacementInfo>();

    /// <summary> Replaces one registered entity with extended version. </summary>
    /// <param name="replacedType">The entity type to be replaced.</param>
    /// <param name="replacementEntityType">The new replacing entity type.</param>
    /// <remarks><para>
    /// This method provides a way to extend entities defined in independently built modules. 
    /// The other use is to integrate the independently developed modules, so that the tables in database 
    /// coming from different modules can actually reference each other through foreign keys.
    /// If the replacement type is not registered with any module, it is placed in the module of the type being replace.  
    /// </para>
    /// </remarks>
    public void ReplaceEntity(Type replacedType, Type replacementEntityType) {
      CheckNotClosed();
      Util.Check(replacedType.IsInterface, "Invalid type: {0}; expected Entity interface.", replacedType);
      Util.Check(replacementEntityType.IsInterface, "Invalid type: {0}; expected Entity interface.",
                       replacementEntityType);
      Util.Check(replacedType.IsAssignableFrom(replacementEntityType),
           "Invalid entity replacement type ({0}), must be derived from type being replaced ({1})", replacementEntityType.Name, replacedType.Name);
      var replInfo = new EntityReplacementInfo() { ReplacedType = replacedType, NewType = replacementEntityType };
      Replacements.Add(replInfo);
    }
    #endregion

    #region Moving entities to other areas
    internal readonly Dictionary<Type, EntityArea> MovedEntities = new Dictionary<Type, EntityArea>();
    /// <summary>Moves entities (types) from their original areas to the target Area. </summary>
    /// <param name="toArea">Target area.</param>
    /// <param name="entityTypes">Entity types.</param>
    public void MoveTo(EntityArea toArea, params Type[] entityTypes) {
      CheckNotClosed();
      foreach(var ent in entityTypes)
        MovedEntities[ent] = toArea;
    }
    internal EntityArea GetNewAreaForEntity(Type entityType) {
      EntityArea area;
      MovedEntities.TryGetValue(entityType, out area);
      return area; 
    }
    #endregion

    #region Sizes
    public void RegisterSize(string code, int size, EntityModule module = null) {
      CheckNotClosed();
      SizeTable[code] = size;
      var fullCode = module.Name + "#" + code;
      SizeTable[fullCode] = size;
    }
    #endregion 

  }//class
}
