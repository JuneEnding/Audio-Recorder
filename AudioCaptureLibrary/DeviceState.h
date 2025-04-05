#pragma once
#include <cstdint>

// NB: All states >= Initialized will allow some methods
        // to be called successfully on the Audio Client
enum class DeviceState : uint8_t
{
    Uninitialized,
    Error,
    Initialized,
    Starting,
    Capturing,
    Stopping,
    Stopped,
};
