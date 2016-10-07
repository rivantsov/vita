SET pver=1.9.0.0
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
nuget push Nupkg\Vita.%pver%.nupkg
nuget push Nupkg\Vita.Web.%pver%.nupkg
nuget push Nupkg\Vita.Modules.%pver%.nupkg
nuget push Nupkg\Vita.Data.MySql.%pver%.nupkg
nuget push Nupkg\Vita.Data.Postgres.%pver%.nupkg
nuget push Nupkg\Vita.Data.SqlCe.%pver%.nupkg
nuget push Nupkg\Vita.Data.SQLite.%pver%.nupkg
pause

:END
endlocal

