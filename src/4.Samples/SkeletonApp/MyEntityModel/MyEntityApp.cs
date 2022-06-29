using System;
using Vita.Entities;

namespace MyEntityModel
{
  public class MyEntityApp : EntityApp
  {
    public MyEntityModule MyModule; 

    public MyEntityApp() : base(nameof(MyEntityApp))
    {
      // Register dbo schema/area; register all your entity modules
      var dbo = AddArea("dbo");
      MyModule = new MyEntityModule(dbo);
    }
  }
}
