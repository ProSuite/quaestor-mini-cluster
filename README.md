# quaestor-mini-cluster
Manages a (local) cluster of processes that are hosting GRPC services. Built for non-container setups in a virtual or bare-metal environment. Quaestor also includes a look-aside load balancer that leverages load reports from the worker services.

## Basic Functionality

Configure a set of processes to be managed by the mini cluster. During a heart beat each process is evaluated to ensure it is running and healthy (serving) using the [GRPC health checking protocol](https://github.com/grpc/grpc/blob/master/doc/health-checking.md). Otherwise the process is re-started.

## Getting Started

### Building from Source

```sh
# Clone the repository:
$ git clone https://github.com/ProSuite/quaestor-mini-cluster.git
$ cd quaestor-mini-cluster
# Build the solution:
$ dotnet build Quaestor.MiniCluster.sln
```

### Hello World

Once the solution has been built, a simple [example cluster](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/examples/HelloWorld/HelloWorld/Program.cs) can be started looking after just one worker process. The example [worker process](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/examples/HelloWorld/WorkerProcess/Program.cs) has 2 arguments: the port at which it shall be serving and optionally the number of seconds after which it will report unhealthy.

```sh
# Run the Hello World example
$ dotnet run --project .\examples\HelloWorld\HelloWorld\HelloWorld.csproj
```

By default the cluster heartbeats every 30 seconds and checks if the process responds and is still healthy. The sample worker process starts reporting as unhealthy after 100 seconds and will then be restarted by quaestor.

Now it's time to check out the Quaestor command line and configuration:

```sh
# Start Quaestor in cluster mode:
$ cd .\src\Quaestor.Console\bin\Debug\net5.0
$ quaestor cluster
```
This starts 4 worker processes and one load balancer process as configured in [quaestor.config.yml](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/src/Quaestor.Console/quaestor.config.yml).

The load balancer service can be tested with the following gRPCurl script (first download [gRPCurl](https://github.com/fullstorydev/grpcurl/releases) and make sure it is in the PATH environment variable):
```sh
# Start a powershell and cd to the quaestor-mini-cluster repository. Then:
$ cd examples
# All available service locations:
$ .\DiscoverServices.ps1 localhost:5150 Worker
# The service location with the lowest current load:
.\DiscoverTopService.ps1 localhost:5150 Worker
```

Stop the cluster (and shutdown the processes) with CTRL+C.

## Load Balancing

The load balancer is a [grpc service](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/src/Quaestor.ServiceDiscovery/service_discovery.proto) that can be used to discover services from a local service registry (i.e. the quaestor.config.yml file) or from [Etcd](https://etcd.io/), a distributed key-value store. The latter allows to discover and balance services from remote, independent clusters and is highly recommended.

In order to use the look-aside load balancer the involved services 

- Must report service health using the GRPC health checking protocol
- Must [report service load](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/src/Quaestor.LoadReporting/load_reporting.proto) as defined in the Quaestor.LoadReporting assembly (see build directory to build the nuget package).

In practice, this could look like this (from the WorkerProcess in the HelloWorld example):

			// The health service every serious grpc server should have:
			var healthService = new HealthServiceImpl();
			healthService.SetStatus(_serviceName, HealthCheckResponse.Types.ServingStatus.Serving);
	
			// The load reporting service required for Quaestor load-balancer:
			LoadReportingGrpcImpl loadReporter = new LoadReportingGrpcImpl();
	
			// Use Load.StartRequest(); at the beginning
			// and Load.StartRequest(); at the end of a request
			// or assign a known load rate using Load.KnownLoadRate
			Load = new ServiceLoad
			{
				ProcessCapacity = 1,
				CurrentProcessCount = 0,
				ServerUtilization = 0.12345
			};
	
			loadReporter.AllowMonitoring("Worker", Load);
	
			var server =
				new Server
				{
					Services =
						{
							// YourGrpc.BindService(yourActualServiceImpl),
							Health.BindService(healthService),
							LoadReportingGrpc.BindService(loadReporter)
						},
					Ports =
					{
						new ServerPort("localhost", port, ServerCredentials.Insecure)
					}
				};
	
			server.Start();

## Bootstrapping using a Windows service

On Windows, the cluster process could be started by a windows service to make sure it is always running. To do so, open an elevated command prompt and run
```sh
# Make sure to replace the bin path and the --configDir directory first.
$ sc create "QuaestorMiniCluster" binPath= "C:\data\git\quaestor-mini-cluster\build\output\Quaestor_0.0.6\quaestor.exe cluster --configDir C:\data\git\quaestor-mini-cluster\build\output\Quaestor_0.0.6" DisplayName= "Quaestor Mini Cluster"
```

The service' startup type could be set to auto and the recovery settings could be set to restart to make sure it will always run.

