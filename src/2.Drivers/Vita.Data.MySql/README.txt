README for VITA MySql Driver.
    1. Add the following options to sql-mode variable in my.ini file (in C:\ProgramData\MySQL\MySQL Server 5.6) 
         ANSI_QUOTES
    2. Connection string must have ' Old Guids=true' to properly handle Guids
    3. MySql supports ordered columns in indexes, but there's no way to get this information when loading index columns from the database.
     So we suppress ordering; we also set all column direction to ASC after construction DbModel
    4. MySql does not have nvarchar; to use unicode, you should set default charset for the database: 
      (from    https://dev.mysql.com/doc/refman/8.0/en/charset-applications.html)
       To create a database such that its tables will use a given default character set and collation for data storage, use a CREATE DATABASE statement like this:

      CREATE DATABASE mydb
        CHARACTER SET latin1
        COLLATE latin1_swedish_ci;

     Specific notes
     MySql supports output params only for stored procs, not for dynamic SQL, so for identities we return inserted identity value 
     using extra SELECT appended to INSERT statement


Note: June 2024
  MySql.Data 8.4.0 (latest) references BouncyCastle.Cryptography 2.2.1, which is marked Vulnerable. 
  As a standard workaround, explicitly added reference to version 2.4.0. May be removed in the future. 