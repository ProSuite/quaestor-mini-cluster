# quaestor-mini-cluster
Manages a (local) cluster of processes that are hosting GRPC services. Built for non-container setups in a virtual or bare-metal environment. Quaestor also includes a look-aside load balancer that leverages load reports from the worker services.

## Basic Functionality

The mini cluster and the load balancer can be used separately but in practice they are more useful if combined.

The **mini cluster** uses a configured set of processes to be observed and kept alive. During a heart beat each process is evaluated to ensure it is running and healthy (serving) using the [GRPC health checking protocol](https://github.com/grpc/grpc/blob/master/doc/health-checking.md). If a process is unhealthy or does not respond the process is re-started.

The **load balancer** is a [grpc service](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/src/Quaestor.ServiceDiscovery/service_discovery.proto) that can be used to discover services from a local service registry (i.e. the quaestor.config.yml file) or from [Etcd](https://etcd.io/), a distributed key-value store. The latter allows to discover and balance services from remote, independent clusters and is highly recommended.

In order to use the look-aside load balancer the involved services 

- Must report service health using the [GRPC health checking protocol](https://github.com/grpc/grpc/blob/master/doc/health-checking.md)
- Must [report service load](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/src/Quaestor.LoadReporting/load_reporting.proto) as defined in the Quaestor.LoadReporting assembly (see build directory to build the nuget package).

The main load balancing logic is encapsulated in a single comparer class, that can be easily exchanged. Thanks to the collected load reports the server utilization and the load for each process can be used to decide on the priority of the service instances.

## Getting Started

### Building from Source

```sh
# Clone the repository:
$ git clone https://github.com/ProSuite/quaestor-mini-cluster.git
$ cd quaestor-mini-cluster
# Build the solution:
$ dotnet build Quaestor.MiniCluster.sln
```

### Mini Cluster: Hello World

Once the solution has been built, a simple [example cluster](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/examples/HelloWorld/HelloWorld/Program.cs) can be started looking after just one worker process. The example [worker process](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/examples/HelloWorld/WorkerProcess/Program.cs) has 2 arguments: the port at which it shall be serving and optionally the number of seconds after which it will report unhealthy.

```sh
# Run the Hello World example
$ dotnet run --project .\examples\HelloWorld\HelloWorld\HelloWorld.csproj
```

By default the cluster heartbeats every 30 seconds and checks if the process responds and is still healthy. The sample worker process starts reporting as unhealthy after 100 seconds and will then be restarted by quaestor.

### Command Line Example

Start the quaestor executable in cluster mode:

```sh
# Start Quaestor in cluster mode:
$ cd .\src\Quaestor.Console\bin\Debug\net6.0
$ quaestor cluster
```
This starts 4 worker processes and one load balancer process as configured in [quaestor.config.yml](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/src/Quaestor.Console/quaestor.config.yml). An alternative directory containing the configuration can be specified on the command line (use quaestor cluster --help for details).

This example also includes a basic load balancer implementation which can be tested using [external monitoring with gRPCurl](### External monitoring with gRPCurl).

Stop the cluster (and shutdown the processes) with CTRL+C.

## Load Balancing

### Requirements for balanced GRPC services

In order for the load balancer to assess the availability and suitability of an individual service end point, the GPRC servers must implement both the GRPC health check protocol and perform load reporting as shown below:

From the [WorkerProcess](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/examples/HelloWorld/WorkerProcess/Program.cs) in the HelloWorld example:

			// The grpc health service every serious grpc server should use:
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

### Load Balancer Command Line

The load balancer can be used independently from the cluster manager. The relevant section in the [quaestor.config.yml](https://github.com/ProSuite/quaestor-mini-cluster/blob/main/src/Quaestor.Console/quaestor.config.yml) is right at the beginning.

```sh
# Start Quaestor in load balancer mode:
$ cd .\src\Quaestor.Console\bin\Debug\net6.0
$ quaestor load-balancer
```

### External monitoring with gRPCurl

The load balancer service that is part of this cluster configuration can be tested with the following gRPCurl script (first download [gRPCurl](https://github.com/fullstorydev/grpcurl/releases) and make sure it is in the PATH environment variable):
```sh
# For this example make sure the quaestor has been started in cluster mode:
$ cd .\src\Quaestor.Console\bin\Debug\net6.0
$ quaestor cluster
# Start a new powershell and cd to the quaestor-mini-cluster repository. Then:
$ cd examples
# All available service locations:
$ .\DiscoverServices.ps1 localhost:5150 Worker
# The service location with the lowest current load:
$ .\DiscoverTopService.ps1 localhost:5150 Worker
# gRPC health check for the first worker service:
$ .\CheckHealth.ps1 localhost:5161 Worker
# gRPC health check for the load balancer service using the configured service name:
$ .\CheckHealth.ps1 localhost:5150 ServiceDiscoveryGrpc
```

## Bootstrapping using a Windows service

On Windows, the cluster process could be started by a windows service to make sure it is always running.
```sh
# Adapt the quaestor.config.yml: Only use absolute paths that can be accessed by the local system account.
# Adapt the logging configuration to contain no relative path or Userprofile environment var.
# Open an elevated command prompt.
# Replace the bin path and the --configDir directory in the following statement.
$ sc create "QuaestorMiniCluster" binPath= "C:\data\git\quaestor-mini-cluster\build\output\Quaestor_0.0.6\quaestor.exe cluster --configDir C:\data\git\quaestor-mini-cluster\build\output\Quaestor_0.0.6" DisplayName= "Quaestor Mini Cluster"
```

The service' startup type could be set to auto and the recovery settings could be set to restart to make sure it will always run.

