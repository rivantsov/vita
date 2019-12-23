using System;
using System.Linq;
using System.Reflection;
using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Tools.Testing {

  public enum TestMethodKind {
    None, Init, Test, Cleanup
  }

  public static class TestUtil {

    public static TestMethodKind GetMethodKind(MethodInfo method) {
      var attrTypes = method.GetCustomAttributes().Select(a => a.GetType());
      var isDisabled = attrTypes.Any(t => t.Name == "IgnoreAttribute");
      if (isDisabled)
        return TestMethodKind.None;
      var isTest = attrTypes.Any(t => t.Name == "TestMethodAttribute" || t.Name == "TestAttribute");
      if (isTest)
        return TestMethodKind.Test;
      var isInit = attrTypes.Any(t => t.Name == "TestInitializeAttribute" || t.Name == "SetUpAttribute");
      if (isInit)
        return TestMethodKind.Init;
      var isCleanup = attrTypes.Any(t => t.Name == "TestCleanupAttribute");
      if (isCleanup)
        return TestMethodKind.Cleanup;
      return TestMethodKind.None;
    }//method

    public static bool IsTestClass(Type testClass) {
      var attrTypes = testClass.GetTypeInfo().GetCustomAttributes().Select(a => a.GetType());
      var isTest = attrTypes.Any(t => t.Name == "TestClassAttribute" || t.Name == "TestFixtureAttribute");
      return isTest;
    }
  
    public static T ExpectFailWith<T>(Action action) where T : Exception {
      try {
        action();
      } catch(Exception ex) {
        if(ex is T)
          return (T) ex;
        throw; 
      }
      Util.Throw("Exception {0} not thrown.", typeof(T));
      return null;
    }

    public static ClientFaultException ExpectClientFault(Action action) {
      return ExpectFailWith<ClientFaultException>(action);
    }
    public static DataAccessException ExpectDataAccessException(Action action) {
      return ExpectFailWith<DataAccessException>(action);
    }

    public static bool EqualsTo(this DateTime x, DateTime y, int precisionMs = 1) {
      return x <= y.AddMilliseconds(precisionMs) && x > y.AddMilliseconds(-precisionMs); 
    }

    /// <summary> Compares arrays of bytes. One use is for comparing row version properties (which are of type byte[]). </summary>
    /// <param name="byteArray">Value to compare.</param>
    /// <param name="other">Value to compare with.</param>
    /// <returns>True if array lengths and byte values match. Otherwise, false.</returns>
    public static bool EqualsTo(this byte[] byteArray, byte[] other) {
      if(byteArray == null && other == null) return true;
      if(byteArray == null || other == null) return false;
      if(byteArray.Length != other.Length)
        return false;
      for(int i = 0; i < byteArray.Length; i++)
        if(byteArray[i] != other[i])
          return false;
      return true;
    }

    public static void EnableTimers(EntityApp app, bool enable) {
      EnableAppTimers(app, enable);
      foreach(var linked in app.LinkedApps)
        EnableAppTimers(linked, enable); 
    }
    private static void EnableAppTimers(EntityApp app, bool enable) {
      var timers = app.GetService<ITimerServiceControl>();
      if(timers != null)
        timers.EnableTimers(enable); 
    }


  }
}
