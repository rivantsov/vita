using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.JobExecution {
  using Vita.Entities.Utilities;

  public partial class JobExecutionModule {
    // delimiter for parameters serialized as json in one text column. 
    public static readonly string JsonArgsDelimiter = Environment.NewLine + new string('"', 4) + Environment.NewLine;

    private JobStartInfo CreateJobStartInfo(LambdaExpression expression, JobThreadType threadType) {
      var info = new JobStartInfo();
      var callExpr = expression.Body as MethodCallExpression;
      Util.Check(callExpr != null, "Invalid Job definition expression - must be a method call: {0}", expression);
      var method = callExpr.Method;
      if(!method.IsStatic) {
        // it is instance method; check that it is defined on one of global objects - module, service, or custom global object
        var obj = this.App.GetGlobalObject(method.DeclaringType);
        Util.Check(obj != null, "Job method {0}.{1} is an instance method, but is defined on object that is not EntityModule, service or any registered global object.",
          method.DeclaringType, method.Name);
      }
      info.Method = method; 
      info.ReturnsTask = info.Method.ReturnType.IsTypeOrSubType(typeof(Task));
      info.ThreadType = threadType;
      info.DeclaringType = method.DeclaringType;
      info.Arguments = new object[callExpr.Arguments.Count];
      for(int i = 0; i < callExpr.Arguments.Count; i++) {
        var argExpr = callExpr.Arguments[i];
        if(argExpr.Type == typeof(JobRunContext))
          continue;
        info.Arguments[i] = ExpressionHelper.Evaluate(argExpr);
      }
      return info;
    }

    private JobStartInfo CreateJobStartInfo(IJobRun jobRun, JobRunContext jobContext) {
      var app = jobContext.App;
      var startInfo = new JobStartInfo();
      var job = jobRun.Job;
      startInfo.ThreadType = job.ThreadType; 
      startInfo.DeclaringType = ReflectionHelper.GetLoadedType(job.DeclaringType, throwIfNotFound: true); //it will throw if cannot find it
      var methName = job.MethodName;
      var argCount = job.MethodParameterCount;
      startInfo.Method = ReflectionHelper.FindMethod(startInfo.DeclaringType, job.MethodName, job.MethodParameterCount);
      // if Method is not static, it must be Module instance - this is a convention
      if(!startInfo.Method.IsStatic)
        startInfo.Object = app.GetGlobalObject(startInfo.DeclaringType); //throws if not found. 
      //arguments
      var prms = startInfo.Method.GetParameters();
      var serArgs = job.Arguments;
      var arrStrArgs = serArgs.Split(new[] { JsonArgsDelimiter }, StringSplitOptions.None);
      Util.Check(prms.Length == arrStrArgs.Length, "Serialized arg count ({0}) in job entity " +
           "does not match the number of target method parameters ({1}); target method: {2}.",
           arrStrArgs.Length, prms.Length, startInfo.Method.Name);
      // Deserialize arguments
      startInfo.Arguments = new object[arrStrArgs.Length];
      for(int i = 0; i < arrStrArgs.Length; i++) {
        var paramType = prms[i].ParameterType;
        if(paramType == typeof(JobRunContext))
          startInfo.Arguments[i] = jobContext;
        else
          startInfo.Arguments[i] = Deserialize(paramType, arrStrArgs[i]);
      }
      return startInfo;
    }

    internal string SerializeArguments(object[] arguments) {
      var serArgs = new string[arguments.Length];
      for(int i = 0; i < arguments.Length; i++) {
        var arg = arguments[i];
        if(arg == null || arg is JobRunContext)
          serArgs[i] = "(JobContext)";
        else
          serArgs[i] = Serialize(arg);
      }
      var result = string.Join(JsonArgsDelimiter, serArgs);
      return result;
    }

    private string Serialize(object value) {
      using(var stream = new MemoryStream()) {
        using(JsonTextWriter jsonTextWriter = new JsonTextWriter(new StreamWriter(stream)) { CloseOutput = false }) {
          _serializer.Serialize(jsonTextWriter, value);
          jsonTextWriter.Flush();
        }
        stream.Position = 0;
        var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return json;
      }
    }//method

    private object Deserialize(Type type, string value) {
      if(string.IsNullOrEmpty(value))
        return ReflectionHelper.GetDefaultValue(type);
      var textReader = new StringReader(value);
      using(JsonTextReader jsonTextReader = new JsonTextReader(textReader)) {
        var obj = _serializer.Deserialize(jsonTextReader, type);
        return obj;
      }
    }//method

    private string CheckJobName(string name) {
      if(name == null)
        return null;
      name = name.Trim();
      Util.Check(name.Length <= _jobNameSize, "Job name too long, must be less than {0} chars.", _jobNameSize);
      return name; 
    }

    private int GetWaitInterval(string intervals, int attemptNumber) {
      Util.Check(attemptNumber >= 2, "AttemptNumber may not be less than 2, cannot retrieve Wait interval.");
      if(string.IsNullOrEmpty(intervals))
        return -1;
      // Attempt number is 1-based; so for attempt 2 the wait time will be the first in the list - 0
      var index = attemptNumber - 2;
      var arr = intervals.Split(',');
      if(index >= arr.Length)
        return -1;
      int result;
      if(int.TryParse(arr[index], out result))
        return result;
      return -1;
    }

    internal static Exception GetInnerMostExc(Exception ex) {
      if(ex == null)
        return null;
      var aggrEx = ex as AggregateException;
      if(aggrEx == null)
        return ex;
      return aggrEx.Flatten().InnerExceptions[0];
    }



  }//class

}
