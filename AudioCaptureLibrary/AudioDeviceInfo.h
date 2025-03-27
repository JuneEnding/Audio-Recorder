#pragma once
#include <string>
#include <Windows.h>

struct AudioDeviceInfo {
    BSTR Id;
    BSTR Name;
    DWORD SampleRate;
    WORD BitsPerSample;
    WORD Channels;
};

struct AudioSessionInfo {
    DWORD ProcessId;
    BSTR DisplayName;
    BSTR IconPath;
    BOOL IsSystemSession;
    BSTR SessionIdentifier;
    BSTR SessionInstanceIdentifier;
};

struct OutputAudioDeviceInfo {
    AudioDeviceInfo DeviceInfo;
    AudioSessionInfo* Sessions;
    INT SessionCount;
};

struct InputAudioDeviceInfo {
    AudioDeviceInfo DeviceInfo;
};
