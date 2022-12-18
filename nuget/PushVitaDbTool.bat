SET pver=3.5.0
Echo Version: "%pver%"
dir packages\vita.vitadbtool.*.nupkg
@echo off
setlocal
:PROMPT
SET AREYOUSURE=N
SET /P AREYOUSURE=Are you sure (Y/[N])?
IF /I "%AREYOUSURE%" NEQ "Y" GOTO END

echo Publishing....
cd packages
:: When we push bin package, the symbols package is pushed automatically by the nuget util
nuget push vita.vitadbtool.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
pause

:END
endlocal

