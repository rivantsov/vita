SET pver=3.4.0
Echo Version: "%pver%"
del /q Nupkg\*.*
nuget.exe pack PackageSpecs\Vita.nuspec -Symbols -version %pver% -outputdirectory Nupkg

pause