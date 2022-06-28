import typing
import grpc
import logging
from quaestor.generated import service_discovery_pb2 as service_util
from quaestor.generated import service_discovery_pb2_grpc as discovery_service
from quaestor.service_location import ServiceLocation


class ServiceDiscovery:
    """
    ServiceDiscovery queries the Quaestor Loadbalancer for available Services.

    :param host_name: host running the grpc discovery service
    :type host_name: str
    :param port_nr: port used for the grpc  discovery service
    :type port_nr: int
    :param channel_credentials: ssl credentials add authentication between client and server and are handled on the channel level
    :type channel_credentials: str
    """

    def __init__(self, host_name: str, port_nr: int, service_name: str,
                 ssl_channel_credentials: str = None, scope: str = "", max_count: int = 1):

        self.host_name = host_name
        self.port_nr = port_nr
        self.scope = scope
        self.service_name = service_name
        self.max_count = max_count
        self.ssl_channel_credentials = ssl_channel_credentials

    def discover_services(self) -> typing.Iterable[ServiceLocation]:
        """
        Provide the service locations for a given service name, ordered randomly.

        :return: Iterator for looping over  ServiceLocation objects
        :rtype: Iterator[ServiceLocation]
        """
        if self.ssl_channel_credentials:
            channel = self._create_secure_channel()
        else:
            channel = grpc.insecure_channel(f'{self.host_name}:{self.port_nr}')

        client = discovery_service.ServiceDiscoveryGrpcStub(channel)
        for location in client.DiscoverServices(self._compile_request()).service_locations:
            yield ServiceLocation(location.service_name, location.host_name, location.port)

    def discover_top_services(self) -> typing.Iterable[ServiceLocation]:
        """
        Provide the best service locations for a given service based on the provided load reports of
        the respective services, ordered according to the implemented load balancing algorithm.

        :return: Iterator for looping over ServiceLocation objects
        :rtype: Iterator[ServiceLocation]
        """
        if self.ssl_channel_credentials:
            channel = self._create_secure_channel()
        else:
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

    def _create_secure_channel(self) -> grpc.Channel:
        channel = grpc.secure_channel(f'{self.host_name}:{self.port_nr}', self.ssl_channel_credentials)
        try:
            grpc.channel_ready_future(channel).result(timeout=5)
            logging.info(f'Successfully established secure channel to {self.host_name}')
        except:
            logging.exception(f'Timeout. Failed to establish secure channel to {self.host_name}')
        return channel
