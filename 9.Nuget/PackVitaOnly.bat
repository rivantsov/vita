SET pver=2.2.1
Echo Version: "%pver%"
del /q Nupkg\*.*
:: Need to delete some MSBuild-generated temp files (with .cs extension)
del /q /s ..\TemporaryGeneratedFile_*.cs
nuget.exe pack PackageSpecs\Vita.nuspec -Symbols -version %pver% -outputdirectory Nupkg

pause