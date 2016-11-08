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


  internal static class JobUtil {
    public static readonly string ArgsDelimiter = Environment.NewLine + new string('"', 4) + Environment.NewLine;

    public static bool IsSet(this JobFlags flags, JobFlags flag) {
      return (flags & flag) != 0;
    }

    public static IJob NewJob(this IEntitySession session, JobDefinition job, JsonSerializer serializer) {
      var ent = session.NewEntity<IJob>();
      ent.Code = job.Code;
      ent.ThreadType = job.ThreadType;
      ent.Id = job.Id;
      ent.Flags = job.Flags;
      var startInfo = job.StartInfo;
      ent.TargetType = startInfo.DeclaringType.Namespace + "." + startInfo.DeclaringType.Name;
      ent.TargetMethod = startInfo.Method.Name;
      ent.TargetParameterCount = startInfo.Arguments.Length;
      ent.Arguments = SerializeArguments(startInfo.Arguments, serializer);
      var rp = job.RetryPolicy;
      ent.RetryIntervalSec = rp.IntervalSeconds;
      ent.RetryCount = rp.RetryCount;
      ent.RetryPauseMinutes = rp.PauseMinutes;
      ent.RetryRoundsCount = rp.RoundsCount;
      if(job.ParentJob != null) {
        ent.ParentJob = session.GetEntity<IJob>(ent.ParentJob.Id, LoadFlags.Stub);
      }
      job.Id = ent.Id;
      return ent;
    }

    public static IJobRun NewJobRun(this IJob job, Guid? sourceId) {
      var session = EntityHelper.GetSession(job);
      var jobRun = session.NewEntity<IJobRun>();
      jobRun.Job = job;
      jobRun.SourceId = sourceId;
      jobRun.CurrentArguments = job.Arguments;
      jobRun.Status = JobRunStatus.Executing;
      jobRun.RemainingRetries = job.RetryCount;
      jobRun.RemainingRounds = job.RetryRoundsCount;
      jobRun.NextStartOn = session.Context.App.TimeService.UtcNow;
      // We use update query to append messages (Log = job.Log + message) - see JobContext class.
      // It does not work if initial value is null; so we initialize it to empty string 
      jobRun.Log = string.Empty;
      return jobRun;
    }


    public static JobStartInfo GetJobStartInfo(Expression<Func<JobRunContext, Task>> expression) {
      var callExpr = expression.Body as MethodCallExpression;
      Util.Check(callExpr != null, "Invalid Job definition expression - must be a method call: {0}", expression);
      var job = new JobStartInfo(); 
      job.Method = callExpr.Method;
      if(job.Method.IsStatic)
        job.DeclaringType = job.Method.DeclaringType; 
      else {
        job.DeclaringType = callExpr.Object.Type; //Object may not be null
        Util.Check(job.DeclaringType.IsSubclassOf(typeof(EntityModule)),
          "Invalid Job definition expression - instance method must be defined on entity module subclass: {0}",
            expression);
      }
      // parameters
      job.Arguments = new object[callExpr.Arguments.Count];
      for(int i = 0; i < callExpr.Arguments.Count; i++) {
        var argExpr = callExpr.Arguments[i];
        if(argExpr.Type == typeof(JobRunContext))
          continue;
        job.Arguments[i] = ExpressionHelper.Evaluate(argExpr);
      }
      return job; 
    }

    internal static string SerializeArguments(object[] arguments, JsonSerializer serializer) {
      var serArgs = new string[arguments.Length];
      for(int i = 0; i < arguments.Length; i++) {
        var arg = arguments[i];
        if(arg == null || arg is JobRunContext)
          continue;
        serArgs[i] = Serialize(serializer, arg);
      }
      var result = string.Join(ArgsDelimiter, serArgs);
      return result;
    }

    public static JobStartInfo GetJobStartInfo(IJobRun jobRun, JobRunContext jobContext, JsonSerializer serializer) {
      var app = jobContext.App; 
      var jobData = new JobStartInfo();
      var job = jobRun.Job; 
      jobData.DeclaringType = ReflectionHelper.GetLoadedType(job.TargetType);
      var methName = job.TargetMethod;
      var argCount = job.TargetParameterCount;
      var bindingFlagsGetAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
      var methods = jobData.DeclaringType.GetMethods(bindingFlagsGetAll)
            .Where(m => m.Name == methName && m.GetParameters().Count() == argCount).ToList();
      Util.Check(methods.Count > 0, "Method {0} not found on type {1}.", methName, jobData.DeclaringType);
      Util.Check(methods.Count < 2, "Found more than one method {0} on type {1}.", methName, jobData.DeclaringType);
      jobData.Method = methods[0];
      // if Method is not static, it must be Module instance - this is a convention
      if(!jobData.Method.IsStatic)
        jobData.Object = app.Modules.First(m => m.GetType() == jobData.DeclaringType);

      //arguments
      var prms = jobData.Method.GetParameters();
      if (prms.Length == 0) {
        jobData.Arguments = new object[0];
        return jobData; 
      }
      var serArgs = jobRun.CurrentArguments;
      Util.Check(!string.IsNullOrEmpty(serArgs),
        "Serialized parameter values not found in job entity, expected {0} values.", prms.Length);
      var arrStrArgs = serArgs.Split(new[] { ArgsDelimiter }, StringSplitOptions.None);
      Util.Check(prms.Length == arrStrArgs.Length, "Serialized arg count ({0}) in job entity " +
           "does not match the number of target method parameters ({1}); target method: {2}.", 
           arrStrArgs.Length, prms.Length, jobData.Method.Name);

      // Deserialize arguments
      jobData.Arguments = new object[arrStrArgs.Length]; 
      for(int i = 0; i < arrStrArgs.Length; i++) {
        var paramType = prms[i].ParameterType;
        if(paramType == typeof(JobRunContext))
          jobData.Arguments[i] = jobContext;
        else 
          jobData.Arguments[i] = Deserialize(serializer, paramType, arrStrArgs[i]);
      }
      return jobData; 
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
