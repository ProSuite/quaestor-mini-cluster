
$xml = [Xml] (Get-Content ..\src\Directory.Build.props)
$version = [Version] $xml.Project.PropertyGroup.Version

$outputDir = ".\output\Quaestor_" + $version

dotnet publish "..\src\Quaestor.Console\Quaestor.Console.csproj" --runtime win-x64 -c Release --self-contained --output $outputDir

pause
