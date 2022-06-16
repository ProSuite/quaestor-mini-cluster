from loadbalancer.generated import service_discovery_pb2 as service_util
from loadbalancer.generated import service_discovery_pb2_grpc as discovery_service
import grpc


class LoadBalancer:
    def __init__(self, host_name: str, port_nr: int, scope: str, service_name: str, max_count: int):
        self.host_name = host_name
        self.port_nr = port_nr
        self.scope = scope
        self.service_name = service_name
        self.max_count = max_count

    def discover_services(self):
        channel = grpc.insecure_channel(f'{self.host_name}:{self.port_nr}')
        client = discovery_service.ServiceDiscoveryGrpcStub(channel)
        for response in client.DiscoverServices(self._compile_request()):
            return response

    def _compile_request(self):
        request = service_util.DiscoverServicesRequest()
        request.scope = self.scope
        request.service_name = self.service_name
        request.max_count = self.max_count
        return request


