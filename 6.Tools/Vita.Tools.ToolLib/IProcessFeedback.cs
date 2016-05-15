using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Tools {

  public enum FeedbackType {
    Info,
    Warning,
    Error,    
  }

  public interface IProcessFeedback {
    void Notify(FeedbackType type, string message, object[] args);
  }

  public static class ProcessFeedbackExtensions {
    public static void WriteLine(this IProcessFeedback feedback) {
      WriteLine(feedback, " ");
    }

    public static void WriteLine(this IProcessFeedback feedback, string message, params object[] args) {
      SendFeedback(feedback, FeedbackType.Info, message, args);
    }
    public static void WriteError(this IProcessFeedback feedback, string message, params object[] args) {
      SendFeedback(feedback, FeedbackType.Error, message, args);
    }
    public static void SendFeedback(this IProcessFeedback feedback, FeedbackType type, string message, params object[] args) {
      if(feedback != null)
        feedback.Notify(type, message, args);
    }
  }

  public class ConsoleProcessFeedback : IProcessFeedback {
    public void Notify(FeedbackType type, string message, object[] args) {
      message = StringHelper.SafeFormat(message, args); 
      switch(type) {
        case FeedbackType.Info:
        case FeedbackType.Warning:
          Console.WriteLine(message);
          break; 
        case FeedbackType.Error:
          var saveColor = Console.ForegroundColor;
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine("ERROR: " + message);
          Console.ForegroundColor = saveColor;
          break; 
      }
    }
  }

  public class TraceProcessFeedback : IProcessFeedback {
    public void Notify(FeedbackType type, string message, object[] args) {
      message = StringHelper.SafeFormat(message, args);
      Trace.WriteLine(message); 
    }
  }


}
