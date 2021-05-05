#
# Checks the health (grpc.Health.V1) for a specific service
#
# Make sure grpcurl.exe (https://github.com/fullstorydev/grpcurl) is in the path
#
# This example actually works while the unit tests in LoadReportingGrpcImplTest are running. 
# 
# Usage:
# CheckHealth.ps1 HOSTNAME:Port ServiceName {pem file with certificate authority}
#
# Example (insecure):
# CheckHealth.ps1 localhost:5150 ServiceDiscoveryGrpc
#
# Example (using TLS):
# CheckHealth.ps1 MY_MACHINE.domain.com:5150 ServiceDiscoveryGrpc "C:\Temp\root_ca.cer"

param (
	$Address = $(throw "Address in the form localhost:5151 is required."),
	$ServiceName = $(throw "Service name is required."),
	$cacert_file
)

Write-host "Checking service health for $ServiceName at address $Address"

$scriptpath = $MyInvocation.MyCommand.Path
$dir = Split-Path $scriptpath
cd $dir
Write-host "Changed directory to $dir"

if ($cacert_file) 
{
	Write-host "TLS: Using specified pem file: " $cacert_file
	$tlsArg="-cacert"
}
else 
{
	Write-host "Using insecure credentials (no TLS)"
	$tlsArg="-plaintext"
}

$requestJson= '{\"service\": \"' + $ServiceName + '\"}'

Write-host "grpcurl response:"
grpcurl.exe -d $requestJson $tlsArg $cacert_file -import-path .\ -proto health.proto $Address grpc.health.v1.Health/Check

Write-Host -NoNewLine 'Press any key to continue...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
