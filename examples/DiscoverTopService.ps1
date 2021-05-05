#
# Gets service locations from the load balancer service for a specific service (TestService)
#
# Make sure grpcurl.exe (https://github.com/fullstorydev/grpcurl) is in the path
#
# This example actually works while the unit tests in LoadReportingGrpcImplTest are running. 
# 
# Usage:
# DiscoverTopService.ps1 HOSTNAME:Port ServiceName {PEM file with certificate authority}
#
# Example (insecure):
# DiscoverTopService.ps1 localhost:5150 QualityVerificationGrpc
#
# Example (using TLS):
# DiscoverTopService.ps1 MY_MACHINE.domain.com:5150 QualityVerificationGrpc "C:\Temp\root_ca.cer"

param (
	$Address = $(throw "Address in the form localhost:5151 is required."),
	$ServiceName = $(throw "Service name is required."),
	$cacert_file
)

Write-host "Discovering services $ServiceName at address $Address"

$scriptpath = $MyInvocation.MyCommand.Path
$dir = Split-Path $scriptpath
cd $dir
Write-host "Changed directory to $dir"

if ($cacert_file) 
{
	Write-host "TLS: Using specified PEM file: " $cacert_file
	$tlsArg="-cacert"
}
else 
{
	Write-host "Using insecure credentials (no TLS)"
	$tlsArg="-plaintext"
}

$serviceNameJson= '{\"service_name\": \"' + $ServiceName + '\", \"max_count\": "1"}'

Write-host "grpcurl response:"
grpcurl.exe -d $serviceNameJson $tlsArg $cacert_file -import-path ..\src\Quaestor.ServiceDiscovery -proto service_discovery.proto $Address Quaestor.ServiceDiscovery.ServiceDiscoveryGrpc/DiscoverTopServices

Write-Host -NoNewLine 'Press any key to continue...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
