README for VITA MySql Driver.
    1. Add the following options to sql-mode variable in my.ini file (in C:\ProgramData\MySQL\MySQL Server 5.6) 
         ANSI_QUOTES
    2. Connection string must have ' Old Guids=true' to properly handle Guids
    3. MySql supports ordered columns in indexes, but there's no way to get this information when loading index columns from the database.
     So we suppress ordering; we also set all column direction to ASC after construction DbModel
