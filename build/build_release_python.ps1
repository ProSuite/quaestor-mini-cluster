# Arbeitsverzeichnis auf den Ort des Skripts setzen
Set-Location $PSScriptRoot

# Name und Pfade
$EnvName = "quaestor-env"
$OutputDir = "..\py\src\quaestor\generated"
$ProcessAdminDir = "..\src\Quaestor.ProcessAdministration"
$ServiceDiscoveryDir = "..\src\Quaestor.ServiceDiscovery"

$ProcessAdminProto = "process_admin.proto"
$ServiceDiscoveryProto = "service_discovery.proto"

# Wenn Umgebung nicht existiert, erstellen
$condaInfo = conda env list | Select-String $EnvName
if (-not $condaInfo) {
    Write-Host "Conda-Umgebung '$EnvName' existiert nicht. Erstelle sie..."
    conda create -y -n $EnvName python=3.11 grpcio-tools -c conda-forge
}

# Ausgabeordner vorbereiten
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -Path $OutputDir -ItemType Directory | Out-Null

# Protobuf-Dateien für process_admin.proto kompilieren
conda run -n $EnvName python -m grpc_tools.protoc `
    --proto_path=$ProcessAdminDir `
    --python_out=$OutputDir `
    --pyi_out=$OutputDir `
    --grpc_python_out=$OutputDir `
    "$ProcessAdminDir\$ProcessAdminProto"

# Protobuf-Dateien für service_discovery.proto kompilieren
conda run -n $EnvName python -m grpc_tools.protoc `
    --proto_path=$ServiceDiscoveryDir `
    --python_out=$OutputDir `
    --pyi_out=$OutputDir `
    --grpc_python_out=$OutputDir `
    "$ServiceDiscoveryDir\$ServiceDiscoveryProto"

Write-Host ""
Write-Host "gRPC-Stubs wurden erfolgreich erstellt unter '$OutputDir'"
Pause
