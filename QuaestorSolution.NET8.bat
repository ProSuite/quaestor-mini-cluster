@cd /d "%~dp0"

set TargetFrameworkVersion=net8.0

%DEVENV% %DEVOPTS% Quaestor.MiniCluster.sln
