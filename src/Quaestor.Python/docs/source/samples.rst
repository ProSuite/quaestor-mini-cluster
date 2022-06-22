Samples
########


Quaestor Installation
=====================

Quaestor is installed with Python Package Manager (pip). Make sure to
run Pip from the python distribution / environment where you need
Quaestor to be installed.

.. code-block:: python

    pip install quaestor.tar.gz



Discover top services
=====================

.. code-block:: python
    :linenos:

    service_discovery = ServiceDiscovery('localhost', 5150, QualityVerificationGrpc, max_count=3)
    for location in service_discovery.discover_top_services():
        print(f'service_name: {location.service_name}\t host_name: {location.host_name}\t port: {location.port} ')
