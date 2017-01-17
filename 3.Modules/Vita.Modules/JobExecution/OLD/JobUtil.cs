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

  public static class JobUtil {
    // delimiter for parameters serialized as json in one text file. 
    public static readonly string JsonArgsDelimiter = Environment.NewLine + new string('"', 4) + Environment.NewLine;

    public static bool IsSet(this JobFlags flags, JobFlags flag) {
      return (flags & flag) != 0;
    }


    public static IJob NewJob(this IEntitySession session, string code, LambdaExpression expression, JsonSerializer serializer) {
      var startInfo = JobStartInfo.Create(expression);
      ValidateJobMethod(session.Context.App, startInfo); 
      var ent = session.NewEntity<IJob>();
      ent.Code = code;
      ent.Flags = JobFlags.None;
      ent.DeclaringType = startInfo.DeclaringType.Namespace + "." + startInfo.DeclaringType.Name;
      ent.MethodName = startInfo.Method.Name;
      ent.MethodParameterCount = startInfo.Arguments.Length;
      ent.Arguments = SerializeArguments(startInfo.Arguments, serializer);
      return ent;
    }

    internal static void ValidateJobMethod(EntityApp app, JobStartInfo startInfo) {
      // We already check return type (Task or Void) in JobDefinition constructor
      // We need to check for instance methods that they are defined on global objects - that can be 'found' easily when it's time to invoke the method.
      var method = startInfo.Method; 
      if (!method.IsStatic) {
        // it is instance method; check that it is defined on one of global objects - module, service, or custom global object
        var obj = app.GetGlobalObject(method.DeclaringType);
        Util.Check(obj != null, "Job/task method {0}.{1} is an instance method, but is defined on object that is not EntityModule, service or any registered global object.", 
          method.DeclaringType, method.Name);
      }
    }

    public static IJobRun NewJobRun(this IJob job, DateTime startOn) {
      var session = EntityHelper.GetSession(job);
      var jobRun = session.NewEntity<IJobRun>();
      jobRun.Job = job;
      jobRun.CurrentArguments = job.Arguments;
      jobRun.Status = JobRunStatus.Executing;
      jobRun.StartOn = startOn;
      // We use update query to append messages (Log = job.Log + message) - see JobContext class.
      // It does not work if initial value is null; so we initialize it to empty string 
      jobRun.Log = string.Empty;
      return jobRun;
    }

    internal static string SerializeArguments(object[] arguments, JsonSerializer serializer) {
      var serArgs = new string[arguments.Length];
      for(int i = 0; i < arguments.Length; i++) {
        var arg = arguments[i];
        if(arg == null || arg is JobRunContext)
          serArgs[i] = "(JobContext)";
        else 
          serArgs[i] = Serialize(serializer, arg);
      }
      var result = string.Join(JsonArgsDelimiter, serArgs);
      return result;
    }

    public static JobStartInfo CreateJobStartInfo(IJobRun jobRun, JobRunContext jobContext) {
      var app = jobContext.App; 
      var jobData = new JobStartInfo();
      var job = jobRun.Job; 
      jobData.DeclaringType = ReflectionHelper.GetLoadedType(job.DeclaringType, throwIfNotFound: true); //it will throw if cannot find it
      var methName = job.MethodName;
      var argCount = job.MethodParameterCount;
      jobData.Method = ReflectionHelper.FindMethod(jobData.DeclaringType, job.MethodName, job.MethodParameterCount); 
      // if Method is not static, it must be Module instance - this is a convention
      if(!jobData.Method.IsStatic)
        jobData.Object = app.GetGlobalObject(jobData.DeclaringType); //throws if not found. 
      //arguments
      var prms = jobData.Method.GetParameters();
      var serArgs = jobRun.CurrentArguments;
      var arrStrArgs = serArgs.Split(new[] { JsonArgsDelimiter }, StringSplitOptions.None);
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
          jobData.Arguments[i] = Deserialize(jobContext.Serializer, paramType, arrStrArgs[i]);
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
