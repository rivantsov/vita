# Vita.Web project and package
The package provides classes for integration with ASP.NET-Core environment. See BooksApiStartup.cs code file for an example of configuring the middleware class. 

## Main classes in the assembly: 
* VitaWebMiddleware - an ASP.NET-core middleware class to be installed into HTTP pipeline. Provides initial handling of the request: sets up OperationContext and WebContext objects - these will be used in the API controllers; handles 'soft exceptions' and converts them into BadRequest response; handles automatic logging of each Web request/response information. 
* VitaJwtTokenHandler - handles Jwt tokens that hold authentication information (user ID); the tokens is created at login and being passed back and forth in HTTP header identifying the currently logged-in user.
* BaseApiController - a base class for API controllers. Handles retreiving the current OperationContext context (associated with the particular Web request, created by the middleware); provides the OpenSession method to open the entity session with the current entity app.  


