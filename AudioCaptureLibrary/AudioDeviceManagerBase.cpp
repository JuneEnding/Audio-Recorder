#include "AudioDeviceManagerBase.h"
#include <comdef.h>
#include <propkey.h>
#include <winapifamily.h>
#include <devpkey.h>
#include <functiondiscoverykeys_devpkey.h>
#include <stdexcept>
#include <Audioclient.h>
#include "Logger.h"

AudioDeviceManagerBase::AudioDeviceManagerBase() : deviceEnumerator(nullptr), stateChangedCallback(nullptr) {
    if (FAILED(CoInitialize(nullptr))) {
        std::string message = "Failed to initialize COM library";
        Logger::GetInstance().Log(message, LogLevel::Error);
        throw std::runtime_error(message);
    }

    if (FAILED(CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_INPROC_SERVER, __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&deviceEnumerator)))) {
        std::string message = "Failed to create MMDeviceEnumerator";
        Logger::GetInstance().Log(message, LogLevel::Error);
        throw std::runtime_error(message);
    }
}

AudioDeviceManagerBase::~AudioDeviceManagerBase() {
    if (deviceEnumerator) {
        deviceEnumerator->Release();
    }
    CoUninitialize();
}

std::vector<wil::com_ptr_nothrow<IMMDevice>> AudioDeviceManagerBase::GetActiveDevices(EDataFlow dataFlow) const
{
    std::vector<wil::com_ptr_nothrow<IMMDevice>> devices = {};
    wil::com_ptr_nothrow<IMMDeviceCollection> deviceCollection = nullptr;

    /*
    // Временно использую только дефолтное устройство
    if (dataFlow == eRender || dataFlow == eCapture) {
        wil::com_ptr_nothrow<IMMDevice> defaultDevice = nullptr;
        HRESULT hr = deviceEnumerator->GetDefaultAudioEndpoint(dataFlow, eConsole, &defaultDevice);
        if (SUCCEEDED(hr)) {
            devices.push_back(defaultDevice);
        }
        else {
            Logger::GetInstance().Log("Failed to get default audio endpoint", LogLevel::Warning);
        }
    }
    return devices;
    */
	
    if (FAILED(deviceEnumerator->EnumAudioEndpoints(dataFlow, DEVICE_STATE_ACTIVE, &deviceCollection))) {
        Logger::GetInstance().Log("Failed to enumerate audio endpoints", LogLevel::Warning);
        return devices;
    }

    UINT count = 0;
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

        devices.push_back(device);
    }

    return devices;
}

std::optional<AudioDeviceInfo> AudioDeviceManagerBase::GetDeviceInfo(wil::com_ptr_nothrow<IMMDevice> device) {
    if (!device) {
        Logger::GetInstance().Log("Invalid device pointer", LogLevel::Warning);
        return std::nullopt;
    }

    wil::com_ptr_nothrow<IPropertyStore> store;
    if (FAILED(device->OpenPropertyStore(STGM_READ, &store))) {
        Logger::GetInstance().Log("Failed to open property store for device", LogLevel::Warning);
        return std::nullopt;
    }

    wil::unique_prop_variant name;
    if (FAILED(store->GetValue(PKEY_Device_FriendlyName, &name))) {
        Logger::GetInstance().Log("Failed to get friendly name for device", LogLevel::Warning);
        return std::nullopt;
    }

    wil::unique_cotaskmem_string deviceId;
    if (FAILED(device->GetId(&deviceId))) {
        Logger::GetInstance().Log("Failed to get device ID", LogLevel::Warning);
        return std::nullopt;
    }

    WAVEFORMATEX* waveFormat = GetDeviceFormat(device);
    if (!waveFormat) {
        Logger::GetInstance().Log("Failed to get device format", LogLevel::Warning);
        return std::nullopt;
    }

    AudioDeviceInfo deviceInfo = {
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

wil::com_ptr_nothrow<IMMDevice> AudioDeviceManagerBase::GetDevice(const std::wstring& deviceId) const
{
    if (deviceId.empty()) {
        Logger::GetInstance().Log("Device ID is empty", LogLevel::Warning);
        return nullptr;
    }

    wil::com_ptr_nothrow<IMMDevice> device;
    if (FAILED(deviceEnumerator->GetDevice(deviceId.c_str(), &device))) {
        Logger::GetInstance().Log("Failed to get device by ID: " + std::string(deviceId.begin(), deviceId.end()), LogLevel::Warning);
        return nullptr;
    }

    return device;
}

std::optional<AudioDeviceInfo> AudioDeviceManagerBase::GetDeviceInfo(const std::wstring& deviceId) {
    auto device = GetDevice(deviceId);
    return GetDeviceInfo(device);
}

DWORD AudioDeviceManagerBase::HashDeviceId(const std::wstring& deviceId) {
    const DWORD fnvOffsetBasis = 2166136261;
    const DWORD fnvPrime = 16777619;

    DWORD hash = fnvOffsetBasis;
    for (wchar_t c : deviceId) {
        hash ^= c;
        hash *= fnvPrime;
    }

    return hash;
}

WAVEFORMATEX* AudioDeviceManagerBase::GetDeviceFormat(wil::com_ptr_nothrow<IMMDevice> device) {
    if (!device) {
        Logger::GetInstance().Log("Device pointer is null", LogLevel::Warning);
        return nullptr;
    }

    wil::com_ptr_nothrow<IAudioClient> audioClient;
    if (FAILED(device->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<void**>(audioClient.put())))) {
        Logger::GetInstance().Log("Failed to activate audio client", LogLevel::Warning);
        return nullptr;
    }

    WAVEFORMATEX* waveFormat = nullptr;
    if (FAILED(audioClient->GetMixFormat(&waveFormat))) {
        Logger::GetInstance().Log("Failed to get mix format", LogLevel::Warning);
        return nullptr;
    }

    return waveFormat;
}

void AudioDeviceManagerBase::RegisterNotificationCallback(DeviceStateChangedCallback callback, EDataFlow flow) {
    stateChangedCallback = callback;

    notificationClient = wil::com_ptr<AudioDeviceNotificationClient>(new AudioDeviceNotificationClient(callback, deviceEnumerator, flow));

    if (FAILED(deviceEnumerator->RegisterEndpointNotificationCallback(notificationClient.get()))) {
        Logger::GetInstance().Log("Failed to register notification callback", LogLevel::Warning);
    }
    else {
        Logger::GetInstance().Log("Notification callback registered successfully", LogLevel::Info);
    }
}

void AudioDeviceManagerBase::UnregisterNotificationCallback() {
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
