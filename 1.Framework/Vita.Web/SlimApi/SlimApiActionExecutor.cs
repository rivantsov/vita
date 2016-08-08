using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Web.SlimApi {
  //copy of WebApi class ActionExecutor with minor adjustments to run for SlimApi methods
  internal class SlimApiActionExecutor
  {
      private readonly Func<object, object[], Task<object>> _executor;
      private static MethodInfo _convertOfTMethod = typeof(SlimApiActionExecutor).GetMethod("Convert", BindingFlags.Static | BindingFlags.NonPublic);
      private static readonly Task<object> _completedTaskReturningNull = Task.FromResult<object>(null);

      public SlimApiActionExecutor(MethodInfo methodInfo)
      {
          _executor = GetExecutor(methodInfo);
      }

      public async Task<object> Execute(object instance, object[] arguments)
      {
          return await _executor(instance, arguments);
      }

      // Method called via reflection.
      private static Task<object> Convert<T>(object taskAsObject)
      {
          Task<T> task = (Task<T>)taskAsObject;
          return CastToObject<T>(task);
      }

      // Do not inline or optimize this method to avoid stack-related reflection demand issues when
      // running from the GAC in medium trust
      [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
      private static Func<object, Task<object>> CompileGenericTaskConversionDelegate(Type taskValueType)
      {

          return (Func<object, Task<object>>)Delegate.CreateDelegate(typeof(Func<object, Task<object>>), _convertOfTMethod.MakeGenericMethod(taskValueType));
      }

      private static Func<object, object[], Task<object>> GetExecutor(MethodInfo methodInfo)
      {
          // Parameters to executor
          ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "instance");
          ParameterExpression parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

          // Build parameter list
          List<Expression> parameters = new List<Expression>();
          ParameterInfo[] paramInfos = methodInfo.GetParameters();
          for (int i = 0; i < paramInfos.Length; i++)
          {
              ParameterInfo paramInfo = paramInfos[i];
              BinaryExpression valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
              UnaryExpression valueCast = Expression.Convert(valueObj, paramInfo.ParameterType);

              // valueCast is "(Ti) parameters[i]"
              parameters.Add(valueCast);
          }

          // Call method
          UnaryExpression instanceCast = (!methodInfo.IsStatic) ? Expression.Convert(instanceParameter, methodInfo.ReflectedType) : null;
          MethodCallExpression methodCall = methodCall = Expression.Call(instanceCast, methodInfo, parameters);

          // methodCall is "((MethodInstanceType) instance).method((T0) parameters[0], (T1) parameters[1], ...)"
          // Create function
          if (methodCall.Type == typeof(void))
          {
              // for: public void Action()
              Expression<Action<object, object[]>> lambda = 
                Expression.Lambda<Action<object, object[]>>(methodCall, instanceParameter, parametersParameter);
              Action<object, object[]> voidExecutor = lambda.Compile();
              return (instance, methodParameters) =>
              {
                  voidExecutor(instance, methodParameters);
                  return _completedTaskReturningNull;

              };
          }
          else
          {
              // must coerce methodCall to match Func<object, object[], object> signature
              UnaryExpression castMethodCall = Expression.Convert(methodCall, typeof(object));
              Expression<Func<object, object[], object>> lambda = 
                    Expression.Lambda<Func<object, object[], object>>(castMethodCall, instanceParameter, parametersParameter);
              Func<object, object[], object> compiled = lambda.Compile();
              if (methodCall.Type == typeof(Task))
              {
                  // for: public Task Action()
                  return (instance, methodParameters) =>
                  {
                      Task r = (Task)compiled(instance, methodParameters);
                      ThrowIfWrappedTaskInstance(methodInfo, r.GetType());
                      return CastToObject(r);
                  };
              }
              else if (typeof(Task).IsAssignableFrom(methodCall.Type))
              {
                  // for: public Task<T> Action()
                  // constructs: return (Task<object>)Convert<T>(((Task<T>)instance).method((T0) param[0], ...))
                  Type taskValueType = GetTaskInnerTypeOrNull(methodCall.Type);
                  var compiledConversion = CompileGenericTaskConversionDelegate(taskValueType);

                  return (instance, methodParameters) =>
                  {
                      object callResult = compiled(instance, methodParameters);
                      Task<object> convertedResult = compiledConversion(callResult);
                      return convertedResult;
                  };
              }
              else
              {
                  // for: public T Action()
                  return (instance, methodParameters) =>
                  {
                      var result = compiled(instance, methodParameters);
                      // Throw when the result of a method is Task. Asynchronous methods need to declare that they
                      // return a Task.
                      Task resultAsTask = result as Task;
                      if (resultAsTask != null)
                      {
                        Util.Throw("Unexpected task instance returned by {0}.{1}", methodInfo.DeclaringType, methodInfo.Name);
                          //throw new InvalidOperationException( Error.InvalidOperation(SRResources.ActionExecutor_UnexpectedTaskInstance,
                          //    methodInfo.Name, methodInfo.DeclaringType.Name);
                      }
                      return Task.FromResult(result);
                  };
              }
          }
      }

      private static void ThrowIfWrappedTaskInstance(MethodInfo method, Type type)
      {
          // Throw if a method declares a return type of Task and returns an instance of Task<Task> or Task<Task<T>>
          // This most likely indicates that the developer forgot to call Unwrap() somewhere.
          // Fast path: check if type is exactly Task first.
          if (type != typeof(Task))
          {
              Type innerTaskType = GetTaskInnerTypeOrNull(type);
              if (innerTaskType != null && typeof(Task).IsAssignableFrom(innerTaskType))
              {
                Util.Throw("Unexpected wrapped task instance returned by {0}.{1}", method.DeclaringType, method.Name);
                //throw Error.InvalidOperation(SRResources.ActionExecutor_WrappedTaskInstance,
                  //    method.Name, method.DeclaringType.Name, type.FullName);
              }
          }
      }//method

      internal static Type GetTaskInnerTypeOrNull(Type type) {
        if(type.IsGenericType && !type.IsGenericTypeDefinition) {
          Type genericTypeDefinition = type.GetGenericTypeDefinition();

          if(typeof(Task<>) == genericTypeDefinition) {
            return type.GetGenericArguments()[0];
          }
        }

        return null;
      }

      internal static async Task<object> CastToObject(Task task) {
        await task;
        return null;
      }
      internal static async Task<object> CastToObject<T>(Task<T> task) {
        return (object)await task;
      }


  }//class
}
