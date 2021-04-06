# quaestor-mini-cluster
Manages a (local) cluster of processes running grpc services. Initially built for non-container environments, such as a physical or virtual windows server. Not quite a consul.

## Basic Functionality

Configure a set of processes to be managed by the mini cluster. During a heart beat each process is evaluated to ensure it is running and healthy (serving). Otherwise the process is re-started.
