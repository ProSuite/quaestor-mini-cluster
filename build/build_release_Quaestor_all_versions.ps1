

$xml = [Xml] (Get-Content ..\src\Directory.Build.props)
$version = [Version] $xml.Project.PropertyGroup.Version

######################################################
# NET 8.0
$netVersion = "net6.0"

.\build_release_Quaestor.ps1 -TargetFramework $netVersion

######################################################
# NET 8.0
$netVersion = "net8.0"

.\build_release_Quaestor.ps1 -TargetFramework $netVersion

# Copy-Item ..\LICENSE -Destination .\output
# Copy-Item ..\README.md -Destination .\output

pause
