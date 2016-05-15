using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Vita.UnitTests.Common {

  public class TestClassInfo {
    public Type TestClass;
    public MethodInfo Init;
    public MethodInfo Cleanup;
    public List<MethodInfo> Tests = new List<MethodInfo>();

    public TestClassInfo(Type testClass) {
      TestClass = testClass;
      Setup();
    }
    public override string ToString() {
      return Tests.Count + " Tests; Init:" + Init + ", Cleanup:" + Cleanup;
    }

    private void Setup() {
      var methods = TestClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);
      var voidMethods = methods.Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0).ToList();
      foreach (var method in voidMethods) {
        var tkind = TestUtil.GetMethodKind(method);
        switch (tkind) {
          case TestMethodKind.Init: this.Init = method; break;
          case TestMethodKind.Test: this.Tests.Add(method); break;
          case TestMethodKind.Cleanup: this.Cleanup = method; break;
        }
      }//foreach
    }

  }//class


}
