# Compiling .proto using protoc
The protoc compiler needs to be run from a console

## Prerequisites
* python must be installed and referenced in the path var
* grpcio-tools package must be installed in the used python env

*command:*
`
python -m grpc_tools.protoc -I C:\git\quaestor-mini-cluster\src\Quaestor.ServiceDiscovery --python_out=src\loadbalancer\generated --grpc_python_out src\loadbalancer\generated C:\git\quaestor-mini-cluster\src\
Quaestor.ServiceDiscovery\service_discovery.proto
`
