# Generated by the gRPC Python protocol compiler plugin. DO NOT EDIT!
"""Client and server classes corresponding to protobuf-defined services."""
import grpc

import process_admin_pb2 as process__admin__pb2


class ProcessAdministrationGrpcStub(object):
    """*
    The shutdown API.
    """

    def __init__(self, channel):
        """Constructor.

        Args:
            channel: A grpc.Channel.
        """
        self.Cancel = channel.unary_unary(
                '/Quaestor.ProcessAdministration.ProcessAdministrationGrpc/Cancel',
                request_serializer=process__admin__pb2.CancelRequest.SerializeToString,
                response_deserializer=process__admin__pb2.CancelResponse.FromString,
                )


class ProcessAdministrationGrpcServicer(object):
    """*
    The shutdown API.
    """

    def Cancel(self, request, context):
        """*
        Cancels an individual (long-running) request.
        """
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details('Method not implemented!')
        raise NotImplementedError('Method not implemented!')


def add_ProcessAdministrationGrpcServicer_to_server(servicer, server):
    rpc_method_handlers = {
            'Cancel': grpc.unary_unary_rpc_method_handler(
                    servicer.Cancel,
                    request_deserializer=process__admin__pb2.CancelRequest.FromString,
                    response_serializer=process__admin__pb2.CancelResponse.SerializeToString,
            ),
    }
    generic_handler = grpc.method_handlers_generic_handler(
            'Quaestor.ProcessAdministration.ProcessAdministrationGrpc', rpc_method_handlers)
    server.add_generic_rpc_handlers((generic_handler,))


 # This class is part of an EXPERIMENTAL API.
class ProcessAdministrationGrpc(object):
    """*
    The shutdown API.
    """

    @staticmethod
    def Cancel(request,
            target,
            options=(),
            channel_credentials=None,
            call_credentials=None,
            insecure=False,
            compression=None,
            wait_for_ready=None,
            timeout=None,
            metadata=None):
        return grpc.experimental.unary_unary(request, target, '/Quaestor.ProcessAdministration.ProcessAdministrationGrpc/Cancel',
            process__admin__pb2.CancelRequest.SerializeToString,
            process__admin__pb2.CancelResponse.FromString,
            options, channel_credentials,
            insecure, call_credentials, compression, wait_for_ready, timeout, metadata)
