class ServiceLocation:
    """
    This is a helper class to wrap the ServiceLocationMsg (which is included in the service-discovery response.
    """
    def __init__(self, service_name: str, host_name: str, port: int):
        self.port = port
        self.host_name = host_name
        self.service_name = service_name
