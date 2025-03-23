#include <comdef.h>
#include <propkey.h>
#include <winapifamily.h>
#include <devpkey.h>
#include <functiondiscoverykeys_devpkey.h>
#include <stdexcept>
#include "InputAudioDeviceManager.h"
#include "Logger.h"

InputAudioDeviceManager::InputAudioDeviceManager() = default;

InputAudioDeviceManager::~InputAudioDeviceManager() = default;

std::vector<InputAudioDeviceInfo> InputAudioDeviceManager::GetActiveInputDevices() {
    std::vector<InputAudioDeviceInfo> inputDevices;

    for (auto& device : GetActiveDevices(eCapture)) {
        auto deviceInfo = GetDeviceInfo(device);
        if (!deviceInfo.has_value()) {
            continue;
        }

        InputAudioDeviceInfo inputInfo;
        inputInfo.DeviceInfo = deviceInfo.value();

        inputDevices.push_back(inputInfo);
    }

    return inputDevices;
}

std::optional<InputAudioDeviceInfo> InputAudioDeviceManager::GetInputDeviceInfo(const std::wstring& deviceId) {
    auto device = GetDevice(deviceId);
    auto deviceInfo = GetDeviceInfo(device);

    if (!deviceInfo.has_value())
        return std::nullopt;

    InputAudioDeviceInfo inputDeviceInfo;
    inputDeviceInfo.DeviceInfo = deviceInfo.value();

    return inputDeviceInfo;
}
