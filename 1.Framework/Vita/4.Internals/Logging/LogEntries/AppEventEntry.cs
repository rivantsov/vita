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
    public string Details = null;
    public Guid? ObjectId = null;
    public string ObjectName = null;
    public int? IntParam = null;

    public AppEventEntry(string category, string eventType, EventSeverity severity, string message, string details = null, 
                         Guid? objectId = null, string objectName = null, int? intParam = null, 
                         LogContext context = null) : base(LogEntryType.AppEvent, context) {
      Category = category;
      EventType = eventType;
      Severity = severity;
      Message = message;
      Details = details;
      ObjectId = objectId;
      ObjectName = objectName;
      IntParam = intParam; 
    }

    private string _asText;
    public override string AsText() {
      _asText = _asText ?? $"--- AppEvent: {Category}/{EventType} ({ObjectName}) {Message}";
      return _asText; 
    }

    public override string ToString() {
      return AsText(); 
    }
  }



}
