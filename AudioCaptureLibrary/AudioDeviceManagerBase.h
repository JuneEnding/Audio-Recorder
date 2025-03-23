#pragma once
#include <vector>
#include <string>
#include <optional>
#include <wil/com.h>
#include <mmdeviceapi.h>
#include "AudioDeviceInfo.h"
#include "AudioDeviceNotificationClient.h"

class AudioDeviceManagerBase {
public:
    AudioDeviceManagerBase();
    virtual ~AudioDeviceManagerBase();

    void RegisterNotificationCallback(DeviceStateChangedCallback callback, EDataFlow flow);
    void UnregisterNotificationCallback();

protected:
    wil::com_ptr_nothrow<IMMDeviceEnumerator> deviceEnumerator;
    wil::com_ptr_nothrow<AudioDeviceNotificationClient> notificationClient;
    DeviceStateChangedCallback stateChangedCallback;

    std::vector<wil::com_ptr_nothrow<IMMDevice>> GetActiveDevices(EDataFlow dataFlow) const;
    wil::com_ptr_nothrow<IMMDevice> GetDevice(const std::wstring& deviceId) const;
    std::optional<AudioDeviceInfo> GetDeviceInfo(const std::wstring& deviceId);
    std::optional<AudioDeviceInfo> GetDeviceInfo(wil::com_ptr_nothrow<IMMDevice> device);
    DWORD HashDeviceId(const std::wstring& deviceId);
    static WAVEFORMATEX* GetDeviceFormat(wil::com_ptr_nothrow<IMMDevice> device);
};
