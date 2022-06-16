from loadbalancer.load_balancer import LoadBalancer

HOSTNAME = 'localhost'
PORTNR = 5150
SCOPE = 'example_cluster'
SERVICENAME = 'ServiceDiscoveryGrpc'


def test_service_discovery():
    lb = LoadBalancer(HOSTNAME, PORTNR, SCOPE, SERVICENAME, 2)
    for response in lb.discover_services():
        print(response)
