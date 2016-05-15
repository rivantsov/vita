using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Common;
using Vita.Entities.Linq;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Authorization {

  /// <summary> An activity grant enabled dynamically from business code for a certain period of time/execution path.</summary>
  /// <remarks>Dynamic activity grants allow enabling certain operations only in specific contexts (parts) of the application. </remarks>
  public class DynamicActivityGrant : ActivityGrant {
    public string DocumentKey;        // The key to save root Document's Id when activity starts
    public AuthorizationFilter InitialCheckFilter; // Filter to check the root entity at activity start
    public string Key; //unique key, used for saving enablement value
    
    private static int _id; //to generate unique key

    public DynamicActivityGrant(Activity activity, AuthorizationFilter filter = null,
           string documentKey = null, AuthorizationFilter initialCheckFilter = null)
      : base(activity, filter) {
      DocumentKey = documentKey;
      InitialCheckFilter = initialCheckFilter;
      Key = "%%AuthDynamicEnablement%%" + Activity.Name;
      Key += "." + (_id++); //to garantee uniqueness
    }

    public void Begin(OperationContext context, object rootEntity = null, object rootId = null) {
      //Check rootEntity against startup filter 
      if (InitialCheckFilter != null) {
        Util.Check(rootEntity != null, "Root entity may not be null when activity is granted with Document filter.");
        var docMatches = InitialCheckFilter.MatchEntity(context, rootEntity);
        if (!docMatches) {
          var msg = StringHelper.SafeFormat("Cannot start dynamic activity {0} - root entity does not pass startup record filter check. Entity: {1}",
            this.Activity.Name, rootEntity);
          var entType = EntityHelper.GetEntityType(rootEntity);
          var session = EntityHelper.GetSession(rootEntity) as SecureSession;
          throw new AuthorizationException(msg, entType, AccessType.Peek, true, null, session);
        }
      }
      //Set root value
      if (DocumentKey != null)
        context.Values[DocumentKey] = rootId;
      ChangeEnabledCounter(context, 1); 
    }

    public void End(OperationContext context) {
      if (DocumentKey != null)
        context.RemoveValue(DocumentKey);
      ChangeEnabledCounter(context, -1);
    }

    // for use in 'using()' statements. ActivityGrant is enabled only for the duration of the block and is disabled at the end automatically. 
    // Ex:    
    //   using (editActivityGrant.Execute(context, payDoc, payDoc.Id )) { 
    //     doStuff(); 
    //   }
    public IDisposable Execute(OperationContext context, object root = null, object rootId = null) {
      var token = new DynamicGrantToken() { Context = context, Grant = this };
      try {
        Begin(context, root, rootId);
        return token; 
      } catch (Exception) {
        token.Dispose();
        throw; 
      }
    }

    public bool IsEnabled(OperationContext context) {
      var counter = GetEnabledCounter(context);
      return counter > 0; 
    }

    //Used at runtime
    public override ActivityGrant CreateSimilarGrant(Activity activity) {
      return new DynamicActivityGrant(activity, this.Filter, this.DocumentKey, this.InitialCheckFilter); 
    }

    public override string ToString() {
      var result = Activity.Name;
      if (Filter != null)
        result += "/" + Filter;
      result += "(dynamic)";
      return result; 
    }

    #region Manipulating enabled counter in Context.Values
    private static object _enablementLock = new object();
    private void ChangeEnabledCounter(OperationContext context, int incrDecr) {
      lock(_enablementLock) {
        var counter = context.GetValue<int>(Key);
        counter = counter + incrDecr;
        if(counter <= 0)
          context.RemoveValue(Key);
        else
          context.SetValue(Key, counter);
      }
    }//method

    private int GetEnabledCounter(OperationContext context) {
      lock(_enablementLock) {
        return context.GetValue<int>(Key);
      }
    }//method
    #endregion

    #region DynamicActivityToken nested class
    // Used by DynamicActivityGrant.Execute method to enable activity inside a 'using' block
    internal class DynamicGrantToken : IDisposable {
      internal OperationContext Context;
      internal DynamicActivityGrant Grant;
      bool _disposed;
      public void Dispose() {
        if (!_disposed)
          Grant.End(Context);
        _disposed = true; 
      }
    }
    #endregion

  }//class

}
