from setuptools import setup

setup(
    name='quaestor',
    version='1.1.1',
    packages=['quaestor', 'quaestor.generated'],
    url='',
    license='',
    author='ESRI Schweiz AG',
    author_email='',
    description='An API to discover services from Quaestor',
    install_requires=["grpcio>1.4.0", "grpcio-tools>1.4.0"]
)
