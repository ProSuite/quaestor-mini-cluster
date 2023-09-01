
$xml = [Xml] (Get-Content ..\src\Directory.Build.props)
$version = [Version] $xml.Project.PropertyGroup.Version

$outputDir = ".\output\Quaestor_" + $version

dotnet publish "..\src\Quaestor.Console\Quaestor.Console.csproj" --runtime win-x64 -c Release --no-self-contained --output $outputDir
dotnet publish "..\src\Quaestor.Cluster.Console\Quaestor.Cluster.Console.csproj" --runtime win-x64 -c Release --no-self-contained --output $outputDir
dotnet publish "..\src\Quaestor.LoadBalancer.Console\Quaestor.LoadBalancer.Console.csproj" --runtime win-x64 -c Release --no-self-contained --output $outputDir

Copy-Item ..\LICENSE -Destination .\output
Copy-Item ..\README.md -Destination .\output

pause
