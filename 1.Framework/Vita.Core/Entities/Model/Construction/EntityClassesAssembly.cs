using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;

using Vita.Common;

namespace Vita.Entities.Model.Construction {

  public class EntityClassesAssembly {
    public AssemblyBuilder AssemblyBuilder;
    public ModuleBuilder ModuleBuilder;

    public EntityClassesAssembly() {
      var assemblyName = "__Vita_EntityClasses";
      var asmName = new AssemblyName(assemblyName);
      asmName.Version = new Version(1, 0, 0, 0);
      AssemblyBuilder = System.AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
      ModuleBuilder = AssemblyBuilder.DefineDynamicModule(assemblyName);
    }
  
  }//class

}//ns
