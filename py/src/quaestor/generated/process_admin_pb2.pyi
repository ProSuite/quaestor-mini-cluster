from google.protobuf import descriptor as _descriptor
from google.protobuf import message as _message
from typing import ClassVar as _ClassVar, Optional as _Optional

DESCRIPTOR: _descriptor.FileDescriptor

class CancelRequest(_message.Message):
    __slots__ = ["environment", "password", "service_name", "user_name"]
    ENVIRONMENT_FIELD_NUMBER: _ClassVar[int]
    PASSWORD_FIELD_NUMBER: _ClassVar[int]
    SERVICE_NAME_FIELD_NUMBER: _ClassVar[int]
    USER_NAME_FIELD_NUMBER: _ClassVar[int]
    environment: str
    password: str
    service_name: str
    user_name: str
    def __init__(self, environment: _Optional[str] = ..., service_name: _Optional[str] = ..., user_name: _Optional[str] = ..., password: _Optional[str] = ...) -> None: ...

class CancelResponse(_message.Message):
    __slots__ = ["success"]
    SUCCESS_FIELD_NUMBER: _ClassVar[int]
    success: bool
    def __init__(self, success: bool = ...) -> None: ...
