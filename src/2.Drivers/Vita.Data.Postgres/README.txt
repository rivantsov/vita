README for Vita Postgres Driver.

VITA uses some extensions, so run the following in PG SQL window: 
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS intarray;


Note June 2024
  Current version of Ngpsql used by Vita is 7.0.7. 
  Version 8.0.3 is avaliable, but using fails, error trying to pass list of Ids as parameter.
  So keeping it at 7.0.7



