#
# Checks the health (grpc.Health.V1) for a specific service
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

Write-host "Checking service health for $ServiceName at address $Address"

$scriptpath = $MyInvocation.MyCommand.Path
$dir = Split-Path $scriptpath
cd $dir
Write-host "Changed directory to $dir"

$requestJson= '{\"service\": \"' + $ServiceName + '\"}'

Write-host "grpcurl response:"
grpcurl.exe -d $requestJson -plaintext -import-path .\ -proto health.proto $Address grpc.health.v1.Health/Check

Write-Host -NoNewLine 'Press any key to continue...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
