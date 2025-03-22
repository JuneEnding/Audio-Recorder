#pragma once
#include <string>
#include <Windows.h>

struct AudioDeviceInfo {
    DWORD PipeId;
    BSTR Id;
    BSTR Name;
    DWORD SampleRate;
    WORD BitsPerSample;
    WORD Channels;
};

struct AudioSessionInfo {
    DWORD PipeId;
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
