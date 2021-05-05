#
# Shows the load report for a specific service (TestService) at a specific address (localhost:5150)
#
# Make sure grpcurl.exe (https://github.com/fullstorydev/grpcurl) is in the path
#
# This example actually works while the unit tests in LoadReportingGrpcImplTest are running. 
# 
# Usage:
# GetLoadReport.ps1 HOSTNAME:Port ServiceName {pem file with certificate authority}
#
# Example (insecure):
# GetLoadReport.ps1 localhost:5161 QualityVerificationGrpc
#
# Example (using TLS):
# GetLoadReport.ps1 MY_MACHINE.domain.com:5161 QualityVerificationGrpc "C:\Temp\root_ca.cer"

param (
	$Address = $(throw "Address in the form localhost:5151 is required."),
	$ServiceName = $(throw "Service name is required."),
	$cacert_file
)

Write-host "Checking load for service $ServiceName at address $Address"

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

$serviceNameJson= '{\"service_name\": \"' + $ServiceName + '\"}'

Write-host "grpcurl response:"
grpcurl.exe -d $serviceNameJson $tlsArg $cacert_file -import-path ..\src\Quaestor.LoadReporting -proto load_reporting.proto $Address Quaestor.LoadReporting.LoadReportingGrpc/ReportLoad

Write-Host -NoNewLine 'Press any key to continue...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
