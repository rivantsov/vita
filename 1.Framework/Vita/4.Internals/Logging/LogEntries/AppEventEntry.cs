using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  public enum EventSeverity {
    Info,  // ex: user login; user logout
    Incident, //ex: login failed
    NeedsAction, //ex: Web request handling took more than 1 second; web request caused 200 SQL queries
  }

  public class AppEventEntry : LogEntry {
    public string Category;
    public string EventType;
    public EventSeverity Severity;
    public string Message;
    public int? IntParam = null;
    public string StringParam = null;
    public Guid? GuidParam;

    public AppEventEntry(LogContext context, string category, string eventType, EventSeverity severity, string message,  
                         int? intParam = null, string stringParam = null, Guid? guidParam = null) : base(context) {
      Category = category;
      EventType = eventType;
      Severity = severity;
      Message = message;
      StringParam = stringParam;
      IntParam = intParam;
      GuidParam = guidParam;
    }

    private string _asText;
    public override string AsText() {
      _asText = _asText ?? $"--- AppEvent: {Category}/{EventType} ({StringParam}) {Message}";
      return _asText; 
    }

    public override string ToString() {
      return AsText(); 
    }
  }



}
