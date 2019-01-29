SET pver=2.1.4
Echo Version: "%pver%"
dir Nupkg\*.nupkg
@echo off
setlocal
:PROMPT
SET AREYOUSURE=N
SET /P AREYOUSURE=Are you sure (Y/[N])?
IF /I "%AREYOUSURE%" NEQ "Y" GOTO END

echo Publishing....
:: When we push bin package, the symbols package is pushed automatically by the nuget util
nuget push Nupkg\Vita.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Nupkg\Vita.Modules.Login.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Nupkg\Vita.Data.MsSql.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Nupkg\Vita.Data.MySql.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Nupkg\Vita.Data.Postgres.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Nupkg\Vita.Data.Oracle.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
nuget push Nupkg\Vita.Data.SQLite.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
pause

:END
endlocal

