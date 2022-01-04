
$xml = [Xml] (Get-Content ..\src\Directory.Build.props)
$version = [Version] $xml.Project.PropertyGroup.Version

$outputDir = ".\output\Quaestor_Test"

dotnet publish "..\src\Quaestor.Console\Quaestor.Console.csproj" --runtime win-x64 -c Release --no-self-contained --output $outputDir

Copy-Item ..\LICENSE -Destination .\output
Copy-Item ..\README.md -Destination .\output

pause
