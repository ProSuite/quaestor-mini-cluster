# Builds the process administration Grpc service to the output directory
# The assembly can be checked into the consuming repo's lib folder.

cd $PSScriptRoot

mode con:cols=200 lines=15000

dotnet build ..\src\Quaestor.ProcessAdministration\Quaestor.ProcessAdministration.csproj -property:Configuration=Release -o .\output\ProcessAdministration

pause
