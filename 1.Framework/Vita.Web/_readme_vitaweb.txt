Brief description of classes:
 
 WebCallContextHandler - creates WebCallContext instance for a web call, logs the information after the call.
 ExceptionHandlingFilter - to handle exceptions thrown in controllers or type formatters, log them and send appropriate response to the client
 BaseApiController - provides basic initialization/integration with VITA framework for WebApi controllers. 
 CheckModelState attribute - to decorate Api controller methods and automatically handle situation when there was deserialization failure of 
    input parameters. By default, Web api continues processing after deserialization failure, passing null as parameter value. 
    The attribute throws ModelStateException if model is invalid. 
 AuthenticatedOnly attribute - marks controller or method as requiring Authenticated user.
 HttpClientWrapper - a convenience wrapper for HttpClient class. It provides a handy automatic setup, convenient GET/PUT/POST methods with 
    with automatic deserialization of returned objects, and nice diagnostic facilities, like checking the HTTP status codes and throwing proper
    exceptions. 

Web call log is defined in Vita.Modules assembly and is exposed as a service. WebCallContext handler uses it to log web call info into the database. 
The log can be used to log all incoming calls. It has two modes - 
  - Basic - only basic information is saved, like datetime, URL, client IP, controller name/method, and duration (in ms) of the call.
  - Details - all info is saved, including input request body (Json or Xml), output message body, request/response headers, and even SQL log.
Web call log automatically switches to Details in case of error - we want to log as much as possible in this case. 
You can set default mode to Basic to save some basic stats for regular calls, just to watch the activity, but for errors full information will be saved 
 - including all SQL calls executed during processing the request (SQL just for this call/client).

Special efforts were made to minimize the performance impact of the logs. Log entries are not saved within the session that handles 
the web request activities, but are pushed into a background async queue (in memory). Background thread flushes accumulated entries 
once a second, using Batch execution mode - so it is a single SQL statement with multiple insert proc calls sent in one roundtrip 
to the database server. 

Look at Vita.UnitTests.Web tests and see different facilities demonstrated in code. After executing the tests, 
look at tables WebCallLogEntry and ErrorLog to see the log entries. Both tables are linked through IDs (without ref integrity)
so if you have an error in ErrorLog table, you can easily find the corresponding Web call log entry with extensive details about the call. 