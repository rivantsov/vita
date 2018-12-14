 SET pver=2.1.1
Echo Version: "%pver%"
dir Nupkg\vita.%pver%.nupkg
@echo off
setlocal
:PROMPT
SET AREYOUSURE=N
SET /P AREYOUSURE= Pushing VITA package. Are you sure (Y/N)?
IF /I "%AREYOUSURE%" NEQ "Y" GOTO END

echo Publishing....
:: When we push bin package, the symbols package is pushed automatically by the nuget util
nuget push Nupkg\Vita.%pver%.nupkg -source https://api.nuget.org/v3/index.json 
pause

:END
endlocal
