import os
import sys
# we need to add the current path (of the package 'generated') to the python paths so that the
# files generated with protoc can be imported.
# reason: protoc does not write fully namespaced import statements
# (no option to add a package name to the import statement). Therefore
# when prosuite is installed, the generated grpc files are not referenced correctly.
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
