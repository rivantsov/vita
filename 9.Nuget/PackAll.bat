SET pver=2.1.4
Echo Version: "%pver%"
del /q Nupkg\*.*
:: Need to delete some MSBuild-generated temp files (with .cs extension)
del /q /s ..\TemporaryGeneratedFile_*.cs
nuget.exe pack PackageSpecs\Vita.nuspec -Symbols -version %pver% -outputdirectory Nupkg
nuget.exe pack PackageSpecs\Vita.Modules.Login.nuspec -Symbols -version %pver% -outputdirectory Nupkg
nuget.exe pack PackageSpecs\Vita.Data.MsSql.nuspec -Symbols -version %pver% -outputdirectory Nupkg
nuget.exe pack PackageSpecs\Vita.Data.MySql.nuspec -Symbols -version %pver% -outputdirectory Nupkg
nuget.exe pack PackageSpecs\Vita.Data.Postgres.nuspec -Symbols -version %pver% -outputdirectory Nupkg
nuget.exe pack PackageSpecs\Vita.Data.Oracle.nuspec -Symbols -version %pver% -outputdirectory Nupkg
nuget.exe pack PackageSpecs\Vita.Data.SQLite.nuspec -Symbols -version %pver% -outputdirectory Nupkg

pause