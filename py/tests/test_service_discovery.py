from quaestor.service_discovery import ServiceDiscovery

HOSTNAME = 'CRASSUS'
PORTNR = 5150
SCOPE = 'example_cluster'
SERVICENAME = 'QualityVerificationGrpc'


def test_discover_services():
    service_discovery = ServiceDiscovery(HOSTNAME, PORTNR, SERVICENAME, max_count=3)
    for location in service_discovery.discover_services():
        print(f'service_name: {location.service_name}\t host_name: {location.host_name}\t port: {location.port} ')


def test_discover_top_services():
    service_discovery = ServiceDiscovery(HOSTNAME, PORTNR, SERVICENAME)
    for location in service_discovery.discover_top_services():
        print(f'service_name: {location.service_name}\t host_name: {location.host_name}\t port: {location.port} ')

