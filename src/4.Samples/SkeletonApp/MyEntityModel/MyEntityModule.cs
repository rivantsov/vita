using System;
using Vita.Entities;

namespace MyEntityModel
{
  public class MyEntityModule : EntityModule
  {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    public MyEntityModule(EntityArea area) : base(area, nameof(MyEntityModule))
    {
      // Register all entity types
      RegisterEntities(typeof(ICustomer));
    }
  }
}
