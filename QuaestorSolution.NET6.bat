@cd /d "%~dp0"

set TargetFrameworkVersion=net6.0

%DEVENV% %DEVOPTS% Quaestor.MiniCluster.sln
