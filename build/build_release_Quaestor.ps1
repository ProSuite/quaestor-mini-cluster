
Param(
	$TargetFramework = 'net8.0'
)

$OutputDir = ".\output\Quaestor_${version}_for_${netVersion}"

Write-Host "`n`Building Quaestor $TargetFramework to $OutputDir *****************************************************************" -ForegroundColor 'Green'

$env:TargetFrameworkVersion="${TargetFramework}"

Write-Host "TargetFrameworkVersion:          ${env:TargetFrameworkVersion}"

dotnet publish "..\src\Quaestor.Cluster.Console\Quaestor.Cluster.Console.csproj" --runtime win-x64 -c Release --no-self-contained --output $OutputDir
dotnet publish "..\src\Quaestor.LoadBalancer.Console\Quaestor.LoadBalancer.Console.csproj" --runtime win-x64 -c Release --no-self-contained --output $OutputDir

Copy-Item ..\LICENSE -Destination $OutputDir
Copy-Item ..\README.md -Destination $OutputDir

