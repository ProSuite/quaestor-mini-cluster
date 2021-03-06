# Generated by the gRPC Python protocol compiler plugin. DO NOT EDIT!
"""Client and server classes corresponding to protobuf-defined services."""
import grpc

import service_discovery_pb2 as service__discovery__pb2


class ServiceDiscoveryGrpcStub(object):
    """*
    A service discovery and one-arm load balancing service.
    """

    def __init__(self, channel):
        """Constructor.

        Args:
            channel: A grpc.Channel.
        """
        self.DiscoverServices = channel.unary_unary(
                '/Quaestor.ServiceDiscovery.ServiceDiscoveryGrpc/DiscoverServices',
                request_serializer=service__discovery__pb2.DiscoverServicesRequest.SerializeToString,
                response_deserializer=service__discovery__pb2.DiscoverServicesResponse.FromString,
                )
        self.DiscoverTopServices = channel.unary_unary(
                '/Quaestor.ServiceDiscovery.ServiceDiscoveryGrpc/DiscoverTopServices',
                request_serializer=service__discovery__pb2.DiscoverServicesRequest.SerializeToString,
                response_deserializer=service__discovery__pb2.DiscoverServicesResponse.FromString,
                )


class ServiceDiscoveryGrpcServicer(object):
    """*
    A service discovery and one-arm load balancing service.
    """

    def DiscoverServices(self, request, context):
        """*
        Provide the service locations for a given service name, ordered randomly.
        """
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')

    def DiscoverTopServices(self, request, context):
        """*
        Provide the best service locations for a given service based on the 
        provided load reports of the respective services, ordered according to the 
        implemented load balancing algorithm.
        """
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')


def add_ServiceDiscoveryGrpcServicer_to_server(servicer, server):
    rpc_method_handlers = {
            'DiscoverServices': grpc.unary_unary_rpc_method_handler(
                    servicer.DiscoverServices,
                    request_deserializer=service__discovery__pb2.DiscoverServicesRequest.FromString,
                    response_serializer=service__discovery__pb2.DiscoverServicesResponse.SerializeToString,
            ),
            'DiscoverTopServices': grpc.unary_unary_rpc_method_handler(
                    servicer.DiscoverTopServices,
                    request_deserializer=service__discovery__pb2.DiscoverServicesRequest.FromString,
                    response_serializer=service__discovery__pb2.DiscoverServicesResponse.SerializeToString,
            ),
    }
    generic_handler = grpc.method_handlers_generic_handler(
            'Quaestor.ServiceDiscovery.ServiceDiscoveryGrpc', rpc_method_handlers)
    server.add_generic_rpc_handlers((generic_handler,))


 # This class is part of an EXPERIMENTAL API.
class ServiceDiscoveryGrpc(object):
    """*
    A service discovery and one-arm load balancing service.
    """

    @staticmethod
    def DiscoverServices(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Quaestor.ServiceDiscovery.ServiceDiscoveryGrpc/DiscoverServices',
            service__discovery__pb2.DiscoverServicesRequest.SerializeToString,
            service__discovery__pb2.DiscoverServicesResponse.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)

    @staticmethod
    def DiscoverTopServices(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Quaestor.ServiceDiscovery.ServiceDiscoveryGrpc/DiscoverTopServices',
            service__discovery__pb2.DiscoverServicesRequest.SerializeToString,
            service__discovery__pb2.DiscoverServicesResponse.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)
