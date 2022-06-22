import typing
import grpc
from quaestor.generated import service_discovery_pb2 as service_util
from quaestor.generated import service_discovery_pb2_grpc as discovery_service
from quaestor.service_location import ServiceLocation

class ServiceDiscovery:
    """
    ServiceDiscovery queries the Quaestor Loadbalancer for available Services.
    """
    def __init__(self, host_name: str, port_nr: int, service_name: str, scope: str = "", max_count: int = 1):
        self.host_name = host_name
        self.port_nr = port_nr
        self.scope = scope
        self.service_name = service_name
        self.max_count = max_count

    def discover_services(self) -> typing.Iterable[ServiceLocation]:
        """
        Provide the service locations for a given service name, ordered randomly.

        :return: Iterator for looping over  ServiceLocation objects
        :rtype: Iterator[
            :class: `ServiceLocation`
            ]
        """
        channel = grpc.insecure_channel(f'{self.host_name}:{self.port_nr}')
        client = discovery_service.ServiceDiscoveryGrpcStub(channel)
        for location in client.DiscoverServices(self._compile_request()).service_locations:
            yield ServiceLocation(location.service_name, location.host_name, location.port)

    def discover_top_services(self) -> typing.Iterable[ServiceLocation]:
        """
        Provide the best service locations for a given service based on the provided load reports of
        the respective services, ordered according to the implemented load balancing algorithm.

        :return: Iterator for looping over ServiceLocation objects
        :rtype: Iterator[:class: `ServiceLocation`]
        """
        channel = grpc.insecure_channel(f'{self.host_name}:{self.port_nr}')
        client = discovery_service.ServiceDiscoveryGrpcStub(channel)
        for location in client.DiscoverTopServices(self._compile_request()).service_locations:
            yield ServiceLocation(location.service_name, location.host_name, location.port)

    def _compile_request(self):
        request = service_util.DiscoverServicesRequest()
        request.scope = self.scope
        request.service_name = self.service_name
        request.max_count = self.max_count
        return request
