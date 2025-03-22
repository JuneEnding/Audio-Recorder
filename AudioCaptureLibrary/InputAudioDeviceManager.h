#pragma once
#include <vector>
#include <string>
#include <optional>
#include <wil/com.h>
#include <mmdeviceapi.h>
#include "AudioDeviceManagerBase.h"

class InputAudioDeviceManager : public AudioDeviceManagerBase {
public:
    InputAudioDeviceManager();
    virtual ~InputAudioDeviceManager();

    std::vector<InputAudioDeviceInfo> GetActiveInputDevices();
    std::optional<InputAudioDeviceInfo> GetInputDeviceInfo(const std::wstring& deviceId);
};
