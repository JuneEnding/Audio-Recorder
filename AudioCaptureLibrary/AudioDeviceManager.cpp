#include "AudioDeviceManager.h"
#include <comdef.h>
#include <propkey.h>
#include <winapifamily.h>
#include <devpkey.h>
#include <functiondiscoverykeys_devpkey.h>
#include <stdexcept>
#include "Logger.h"

AudioDeviceManager::AudioDeviceManager() : deviceEnumerator(nullptr), stateChangedCallback(nullptr) {
    if (FAILED(CoInitialize(nullptr))) {
        std::string message = "Failed to initialize COM library";
        Logger::GetInstance().Log(message, LogLevel::Error);
        throw std::runtime_error(message);
    }

    if (FAILED(CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&deviceEnumerator)))) {
        std::string message = "Failed to create MMDeviceEnumerator";
        Logger::GetInstance().Log(message, LogLevel::Error);
        throw std::runtime_error(message);
    }
}

AudioDeviceManager::~AudioDeviceManager() {
    if (deviceEnumerator) {
        deviceEnumerator->Release();
    }
    CoUninitialize();
}

std::vector<NativeAudioDeviceInfo> AudioDeviceManager::GetActiveAudioDevices() {
    std::vector<NativeAudioDeviceInfo> devices = {};

    wil::com_ptr_nothrow<IMMDeviceCollection> deviceCollection = nullptr;
    if (FAILED(deviceEnumerator->EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, &deviceCollection))) {
        Logger::GetInstance().Log("Failed to enumerate audio endpoints", LogLevel::Warning);
        return devices;
    }

    UINT count;
    if (FAILED(deviceCollection->GetCount(&count))) {
        Logger::GetInstance().Log("Failed to get device count", LogLevel::Warning);
        return devices;
    }

    for (UINT i = 0; i < count; ++i) {
        wil::com_ptr_nothrow<IMMDevice> device = nullptr;
        if (FAILED(deviceCollection->Item(i, &device))) {
            Logger::GetInstance().Log("Failed to get device from collection", LogLevel::Warning);
            continue;
        }

        auto deviceInfo = GetAudioDeviceInfo(device);
        if (deviceInfo) {
            devices.push_back(*deviceInfo);
        }
    }

    return devices;
}

std::optional<NativeAudioDeviceInfo> AudioDeviceManager::GetAudioDeviceInfo(wil::com_ptr_nothrow<IMMDevice> device) {
    if (!device) {
        Logger::GetInstance().Log("Invalid device pointer", LogLevel::Warning);
        return std::nullopt;
    }

    wil::com_ptr_nothrow<IPropertyStore> store;
    HRESULT hr = device->OpenPropertyStore(STGM_READ, &store);
    if (FAILED(hr)) {
        Logger::GetInstance().Log("Failed to open property store for device", LogLevel::Warning);
        return std::nullopt;
    }

    wil::unique_prop_variant name;
    hr = store->GetValue(PKEY_Device_FriendlyName, &name);
    if (FAILED(hr)) {
        Logger::GetInstance().Log("Failed to get friendly name for device", LogLevel::Warning);
        return std::nullopt;
    }

    wil::unique_cotaskmem_string deviceId;
    hr = device->GetId(&deviceId);
    if (FAILED(hr)) {
        Logger::GetInstance().Log("Failed to get device ID", LogLevel::Warning);
        return std::nullopt;
    }

    WAVEFORMATEX* waveFormat = GetDeviceFormat(device);
    if (!waveFormat) {
        Logger::GetInstance().Log("Failed to get device format", LogLevel::Warning);
        return std::nullopt;
    }

    NativeAudioDeviceInfo deviceInfo = {
        HashDeviceId(deviceId.get()),
        SysAllocString(deviceId.get()),
        SysAllocString(name.pwszVal),
        waveFormat->nSamplesPerSec,
        waveFormat->wBitsPerSample,
        waveFormat->nChannels
    };

    CoTaskMemFree(waveFormat);

    return deviceInfo;
}

std::optional<NativeAudioDeviceInfo> AudioDeviceManager::GetAudioDeviceInfo(const std::wstring& deviceId) {
    if (deviceId.empty()) {
        Logger::GetInstance().Log("Device ID is empty", LogLevel::Warning);
        return std::nullopt;
    }

    wil::com_ptr_nothrow<IMMDevice> device;
    HRESULT hr = deviceEnumerator->GetDevice(deviceId.c_str(), &device);
    if (FAILED(hr)) {
        Logger::GetInstance().Log("Failed to get device by ID: " + std::string(deviceId.begin(), deviceId.end()), LogLevel::Warning);
        return std::nullopt;
    }

    return GetAudioDeviceInfo(device);
}

void AudioDeviceManager::RegisterNotificationCallback(DeviceStateChangedCallback callback) {
    stateChangedCallback = callback;

    notificationClient = wil::com_ptr<AudioDeviceNotificationClient>(new AudioDeviceNotificationClient(callback));

    if (FAILED(deviceEnumerator->RegisterEndpointNotificationCallback(notificationClient.get()))) {
        Logger::GetInstance().Log("Failed to register notification callback", LogLevel::Warning);
    } else {
        Logger::GetInstance().Log("Notification callback registered successfully", LogLevel::Info);
    }
}

void AudioDeviceManager::UnregisterNotificationCallback() {
    if (notificationClient) {
        if (FAILED(deviceEnumerator->UnregisterEndpointNotificationCallback(notificationClient.get()))) {
            Logger::GetInstance().Log("Failed to unregister notification callback", LogLevel::Warning);
        }
        else {
            Logger::GetInstance().Log("Notification callback unregistered successfully", LogLevel::Info);
        }
        notificationClient.reset();
    }
    stateChangedCallback = nullptr;
}

DWORD AudioDeviceManager::HashDeviceId(const std::wstring& deviceId) {
    const DWORD fnvOffsetBasis = 2166136261;
    const DWORD fnvPrime = 16777619;

    DWORD hash = fnvOffsetBasis;
    for (wchar_t c : deviceId) {
        hash ^= c;
        hash *= fnvPrime;
    }

    return hash;
}

WAVEFORMATEX* AudioDeviceManager::GetDeviceFormat(wil::com_ptr_nothrow<IMMDevice> device) {
    if (!device) {
        Logger::GetInstance().Log("Device pointer is null", LogLevel::Warning);
        return nullptr;
    }

    wil::com_ptr_nothrow<IAudioClient> audioClient;
    HRESULT hr = device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(audioClient.put()));
    if (FAILED(hr)) {
        Logger::GetInstance().Log("Failed to activate audio client", LogLevel::Warning);
        return nullptr;
    }

    WAVEFORMATEX* waveFormat = nullptr;
    hr = audioClient->GetMixFormat(&waveFormat);
    if (FAILED(hr)) {
        Logger::GetInstance().Log("Failed to get mix format", LogLevel::Warning);
        return nullptr;
    }

    return waveFormat;
}
