README for Vita Postgres Driver.

The Ngpsql dependency is set to exactly [2.1.3] (no higher) - older version. 
With later versions (2.2.5) there are problems - creating NpgsqlConnection takes several seconds; (note: creating connection object, not even opening connection). 
Any attempts to fix the problem failed, so for now setting dependency to fixed version.  

VITA uses some extensions, so run the following in PG SQL window: 
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS intarray;


