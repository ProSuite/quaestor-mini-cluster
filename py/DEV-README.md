# Compiling .proto using protoc
The protoc compiler needs to be run from a console

## Prerequisites
* instead of using python from that is defined in the path var, use the specific python.exe that is referring to the target env. For example: 
  `C:\\Users\\sts\\AppData\\Local\\ESRI\\conda\\envs\\arcgispro-py3-clone\python.exe 
`
* grpcio-tools package must be installed in the target python env
  if not installed: install it: `python -m pip install grpcio`


*command:*
`
python -m grpc_tools.protoc -I C:\git\quaestor-mini-cluster\src\Quaestor.ServiceDiscovery --python_out=.\src\quaestor\generated --grpc_python_out .\src\quaestor\generated C:\git\quaestor-mini-cluster\src\Quaestor.ServiceDiscovery\service_discovery.proto
`

## Post compile requirements
if the generated code is within a package or subfolder, the path to the generated code files needs to be added to the python paths. 
Otherwise files generated with protoc can be imported. 
 
Reason: protoc does not write fully namespaced import statements (no option to add a package name to the import statement). Therefore
when prosuite is installed, the generated grpc files are not referenced correctly.

The necessary code to add the path to sys.path could be placed in the __init__.py file of the package:
`sys.path.append(os.path.dirname(os.path.abspath(__file__)))`

# Installing quaestor
#### installing in dev mode (does install a egg link only)
`PS C:\git\quaestor-mini-cluster\src\Quaestor.Python> C:\Users\sts\AppData\Local\ESRI\conda\envs\arcgispro-py3-clone\scripts\pip install -e "./src"
`

##### installing into an environment (does install the package)
`PS C:\git\quaestor-mini-cluster\src\Quaestor.Python\src> python setup.py sdist`
