This is a sample Web application built on AngularJS platform and demonstrating the use of VITA framework to implement the server-side functionality.
The app is contributed by Dave Clemmer (moplus user). 
The app uses two MS SQL databases: VitaBooksUI, VitaBooksLogs - make sure these databases exist on local host before you run the app. Verify the connection string settings in Web.Config file, make sure they match the location of the databases. 
Initially the databases are empty. At startup the app will create the database structure (tables, procs, etc), and then proceed to import around 250 books from Google Books API. It might take up to a minute of extra time at startup, before the home page appears. After that, the startup will be fast. 
Read about some features to explore in the app on the Home page. 
Enjoy it!