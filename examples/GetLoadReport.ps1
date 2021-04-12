#
# Shows the load report for a specific service (TestService) at a specific address (localhost:5150)
#
# Make sure grpcurl.exe (https://github.com/fullstorydev/grpcurl) is in the path
#
# This example actually works while the unit tests in LoadReportingGrpcImplTest are running. 
# Otherwise change the address and possibly switch from -plaintext to -cacert
#

param (
	$Address = $(throw "Address in the form localhost:5151 is required."),
	$ServiceName = $(throw "Service name is required.")
)

Write-host "Checking load for service $ServiceName at address $Address"

$scriptpath = $MyInvocation.MyCommand.Path
$dir = Split-Path $scriptpath
cd $dir
Write-host "Changed directory to $dir"

$serviceNameJson= '{\"service_name\": \"' + $ServiceName + '\"}'

Write-host "grpcurl response:"
grpcurl.exe -d $serviceNameJson -plaintext -import-path ..\src\Quaestor.Microservices.Definitions -proto load_reporting.proto $Address Quaestor.Microservices.Definitions.LoadReportingGrpc/ReportLoad

Write-Host -NoNewLine 'Press any key to continue...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
