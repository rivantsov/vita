SET pver=4.1.0
Echo Version: "%pver%"
dir packages\*.nupkg
@echo off
setlocal
:PROMPT
SET AREYOUSURE=N
SET /P AREYOUSURE=Are you sure (Y/[N])?
IF /I "%AREYOUSURE%" NEQ "Y" GOTO END

echo Publishing....
cd packages
:: When we push bin package, the symbols package is pushed automatically by the nuget util
nuget push Vita.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Vita.Data.MsSql.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Vita.Data.MySql.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Vita.Data.Postgres.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Vita.Data.Oracle.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Vita.Data.SQLite.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
pause

:END
endlocal

