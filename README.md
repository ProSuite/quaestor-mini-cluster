# quaestor-mini-cluster
Manages a (local) cluster of processes running GRPC services. Initially built for non-container environments, such as a physical or virtual windows server. Not quite a consul.

## Basic Functionality

Configure a set of processes to be managed by the mini cluster. During a heart beat each process is evaluated to ensure it is running and healthy (serving). Otherwise the process is re-started.

## Getting Started

### Hello World

Build the solution and run "examples\HelloWorld\HelloWorld\bin\Debug\net5.0\HelloWorld.exe" on the command line. This will start a single, separate worker process and check its health every 30 seconds. The worker process will become un-healthy after 75 seconds which triggers a re-start by the cluster.

### Configurable Cluster

For a configurable, more real-world example, run the "Quaestor.MiniCluster.Guardian.exe" example from the examples\ConfigurableCluster\Quaestor.MiniCluster.Guardian project. This executable can be registered and run as a windows service. To do so, open an elevated command prompt and run

sc.exe create "Questor Sample Service" binPath=<path to Quaestor.MiniCluster.Guardian.exe>

