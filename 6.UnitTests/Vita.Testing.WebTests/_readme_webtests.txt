These unit tests provide illustration/tests for diagnostic and helper objects in Vita.Web assembly. See readme file there for more info about Vita.Web assembly.
The test is setup to run with MS SQL database only, but switching to other servers should not be difficult. 
You must create VitaBooks, VitaBooksLogs databases on target server before you run the tests. Adjust the connection strings in AppConfig accordingly. 
Test suite creates server-side Book data service (RESTful Json-based service) and uses Http client (HttpClientWrapper) to execute test Web api calls. 
For authentication the service uses Authorization header (Jwt token). The token is returned from successful Login call.
This header must be supplied with any service call that requires authenticated user. 


