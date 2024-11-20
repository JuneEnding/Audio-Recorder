#pragma once

#include <vector>
#include <string>
#include <wil/com.h>
#include <functional>
#include <Windows.h>
#include <mmdeviceapi.h>
#include <Audioclient.h>
#include <optional>
#include "AudioDeviceNotificationClient.h"

struct NativeAudioDeviceInfo {
    DWORD PipeId;
    BSTR Id;
    BSTR Name;
    DWORD SampleRate;
    WORD BitsPerSample;
    WORD Channels;
};

class AudioDeviceManager {
public:
    AudioDeviceManager();
    ~AudioDeviceManager();

    std::vector<NativeAudioDeviceInfo> GetActiveAudioDevices();
    std::optional<NativeAudioDeviceInfo> GetAudioDeviceInfo(const std::wstring& deviceId);
    void RegisterNotificationCallback(DeviceStateChangedCallback callback);
    void UnregisterNotificationCallback();

private:
    wil::com_ptr_nothrow<IMMDeviceEnumerator> deviceEnumerator;
    wil::com_ptr_nothrow<AudioDeviceNotificationClient> notificationClient;
    DeviceStateChangedCallback stateChangedCallback;

    std::optional<NativeAudioDeviceInfo> GetAudioDeviceInfo(wil::com_ptr_nothrow<IMMDevice> device);
    DWORD HashDeviceId(const std::wstring& deviceId);
    WAVEFORMATEX* GetDeviceFormat(wil::com_ptr_nothrow<IMMDevice> device);
};
