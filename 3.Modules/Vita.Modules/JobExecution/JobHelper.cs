using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {


  public static class JobHelper {
    public static readonly string ArgsDelimiter = Environment.NewLine + new string('"', 4) + Environment.NewLine;

    public static bool IsSet(this JobFlags flags, JobFlags flag) {
      return (flags & flag) != 0;
    }

    public static JobStartInfo ParseCallExpression(Expression<Action> expression, JsonSerializer serializer) {
      var callExpr = expression.Body as MethodCallExpression;
      Util.Check(callExpr != null, "Invalid Job definition expression - must be a method call: {0}", expression);
      var job = new JobStartInfo(); 
      job.Method = callExpr.Method;
      if(job.Method.IsStatic)
        job.TargetType = job.Method.DeclaringType; 
      else {
        job.TargetType = callExpr.Object.Type; //Object may not be null
        Util.Check(job.TargetType.IsSubclassOf(typeof(EntityModule)),
          "Invalid Job definition expression - instance method must be defined on entity module subclass: {0}",
            expression);
      }
      //parameters
      job.Arguments = new object[callExpr.Arguments.Count];
      job.SerializedArguments = new string[callExpr.Arguments.Count];
      for(int i = 0; i < callExpr.Arguments.Count; i++) {
        var argExpr = callExpr.Arguments[i];
        if(argExpr.Type == typeof(JobContext))
          continue;
        var arg = job.Arguments[i] = ExpressionHelper.Evaluate(argExpr);
        if(arg != null)
          job.SerializedArguments[i] = Serialize(serializer, arg); 
      }
      return job; 
    }

    public static JobStartInfo GetJobInfo(this IJob job, JsonSerializer serializer, JobContext jobContext) {
      var jobInfo = new JobStartInfo();
      jobInfo.TargetType = ReflectionHelper.GetLoadedType(job.TargetType);
      var methName = job.TargetMethod;
      var argCount = job.TargetParameterCount;
      var methods = jobInfo.TargetType.GetMethods()
            .Where(m => m.Name == methName && m.GetParameters().Count() == argCount).ToList();
      Util.Check(methods.Count > 0, "Method {0} not found on type {1}.", methName, jobInfo.TargetType.Name);
      Util.Check(methods.Count < 2, "Found more than one method {0} on type {1}.", methName, jobInfo.TargetType.Name);
      jobInfo.Method = methods[0];
      //arguments
      var prms = jobInfo.Method.GetParameters();
      if (prms.Length == 0) {
        jobInfo.Arguments = new object[0];
        return jobInfo; 
      }
      var serArgs = job.SerializedArguments;
      Util.Check(!string.IsNullOrEmpty(serArgs),
        "Serialized parameter values not found in job entity, expected {0} values.", prms.Length);
      var serArr = serArgs.Split(new[] { ArgsDelimiter }, StringSplitOptions.None);
      Util.Check(prms.Length == serArgs.Length, "Serialized arg count ({0}) in job entity " +
           "does not match the number of target method parameters {1} method: {2}.", 
           serArr.Length, prms.Length, jobInfo.Method.Name);
      jobInfo.Arguments = new object[serArgs.Length]; 
      for(int i = 0; i < serArgs.Length; i++) {
        var paramType = prms[i].ParameterType;
        if(paramType == typeof(JobContext))
          jobInfo.Arguments[i] = jobContext;
        else 
          jobInfo.Arguments[i] = Deserialize(serializer, paramType, serArr[i]);
      }
      return jobInfo; 
    }

    public static IJob NewJob(this IEntitySession session, string code, JobStartInfo job, int retryCount = 3, int retryInterval = 5, 
        Guid? ownerId = null) {
      var ent = session.NewEntity<IJob>();
      ent.Code = code;
      ent.Status = JobStatus.Pending;
      ent.TargetType = job.TargetType.Namespace + "." + job.TargetType.Name;
      ent.TargetMethod = job.Method.Name;
      ent.TargetParameterCount = job.Arguments.Length;
      ent.SerializedArguments = string.Join(ArgsDelimiter, job.SerializedArguments);
      ent.RetryCount = retryCount;
      ent.RetryIntervalMinutes = retryInterval;
      ent.OwnerId = ownerId;
      return ent;
    }

    private static string Serialize(JsonSerializer serializer, object value) {
      using(var stream = new MemoryStream()) {
        using(JsonTextWriter jsonTextWriter = new JsonTextWriter(new StreamWriter(stream)) { CloseOutput = false }) {
          serializer.Serialize(jsonTextWriter, value);
          jsonTextWriter.Flush();
        }
        stream.Position = 0;
        var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return json;
      }
    }//method

    private static object Deserialize(JsonSerializer serializer, Type type, string value) {
      if(string.IsNullOrEmpty(value))
        return ReflectionHelper.GetDefaultValue(type); 
      var textReader = new StringReader(value);
      using(JsonTextReader jsonTextReader = new JsonTextReader(textReader)) {
        var obj = serializer.Deserialize(jsonTextReader, type);
        return obj;
      }
    }//method


  }//class
}//ns
