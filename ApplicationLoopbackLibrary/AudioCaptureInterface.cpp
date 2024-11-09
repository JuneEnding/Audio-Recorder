// AudioCaptureInterface.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <wrl.h>
#include <iostream>
#include <map>
#include <string>
#include <chrono>
#include <sstream>
#include <iomanip>
#include <memory>
#include <comutil.h>
#include <vector>
#include <propkey.h>
#include <winapifamily.h>
#include <devpkey.h>
#include <functiondiscoverykeys_devpkey.h>
#include <string>
#include <functional>
#include <fstream>

#include "pch.h"
#include "ApplicationLoopbackCapture.h"
#include "AudioDeviceCapture.h"


extern "C" {
    struct NativeAudioDeviceInfo {
        DWORD PipeId;
        BSTR Id;
        BSTR Name;
        DWORD SampleRate;
        WORD BitsPerSample;
        WORD Channels;
    };

    __declspec(dllexport) NativeAudioDeviceInfo* GetAudioDevices(int *deviceCount);
    __declspec(dllexport) void FreeAudioDevicesArray(NativeAudioDeviceInfo* devices, int deviceCount);
    __declspec(dllexport) long long StartCapture(int *pids, int count, NativeAudioDeviceInfo *audioDevices, int deviceCount);
    __declspec(dllexport) void StopCapture(int *pids, int count, NativeAudioDeviceInfo* audioDevices, int deviceCount);
}

std::map<DWORD, ComPtr<ApplicationLoopbackCapture>> activeCaptures;
std::map<DWORD, std::unique_ptr<AudioDeviceCapture>> activeDeviceCaptures;

DWORD GetDeviceIdHash(const std::wstring& deviceId) {
    DWORD hash = 2166136261u;

    for (wchar_t ch : deviceId) {
        hash ^= ch;
        hash *= 16777619u;
    }

    return hash;
}

NativeAudioDeviceInfo* GetAudioDevices(int* deviceCount) {
    std::vector<NativeAudioDeviceInfo> devices = {};

    wil::com_ptr_nothrow<IMMDeviceEnumerator> enumerator;
    if (SUCCEEDED(CoCreateInstance(__uuidof(MMDeviceEnumerator), NULL, CLSCTX_ALL, IID_PPV_ARGS(&enumerator)))) {
        wil::com_ptr_nothrow<IMMDeviceCollection> deviceCollection;
        if (SUCCEEDED(enumerator->EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, &deviceCollection))) {
            UINT count;
            if (SUCCEEDED(deviceCollection->GetCount(&count))) {
                for (UINT i = 0; i < count; ++i) {
                    wil::com_ptr_nothrow<IMMDevice> device;
                    if (SUCCEEDED(deviceCollection->Item(i, &device))) {
                        wil::unique_prop_variant name;
                        wil::com_ptr_nothrow<IPropertyStore> store;
                        if (SUCCEEDED(device->OpenPropertyStore(STGM_READ, &store)) &&
                            SUCCEEDED(store->GetValue(PKEY_Device_FriendlyName, &name))) {
                            wil::unique_cotaskmem_string deviceId;
                            if (SUCCEEDED(device->GetId(&deviceId))) {
                                PWSTR id = deviceId.get();
                                DWORD deviceHash = GetDeviceIdHash(id);

                                wil::com_ptr_nothrow<IAudioClient> audioClient;
                                if (SUCCEEDED(device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, NULL, reinterpret_cast<void**>(&audioClient)))) {
                                    WAVEFORMATEX* waveFormat;
                                    if (SUCCEEDED(audioClient->GetMixFormat(&waveFormat))) {
                                        DWORD sampleRate = waveFormat->nSamplesPerSec;
                                        WORD bitsPerSample = waveFormat->wBitsPerSample;
                                        WORD channels = waveFormat->nChannels;

                                        NativeAudioDeviceInfo deviceInfo = { deviceHash, SysAllocString(id), SysAllocString(name.pwszVal), sampleRate, bitsPerSample, channels };
                                        devices.push_back(deviceInfo);

                                        CoTaskMemFree(waveFormat);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    *deviceCount = static_cast<int>(devices.size());
    NativeAudioDeviceInfo* devicesArray = new NativeAudioDeviceInfo[*deviceCount];
    std::copy(devices.begin(), devices.end(), devicesArray);

    return devicesArray;
}

void FreeAudioDevicesArray(NativeAudioDeviceInfo* devices, int deviceCount) {
    for (int i = 0; i < deviceCount; ++i) {
        SysFreeString(devices[i].Name);
    }
    delete[] devices;
}

long long GenerateUniqueId() {
    auto now = std::chrono::system_clock::now();

    auto duration = now.time_since_epoch();
    auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();

    return millis;
}

long long StartCapture(int* pids, int count, NativeAudioDeviceInfo* audioDevices, int deviceCount) {
    auto captureId = GenerateUniqueId();

    for (int i = 0; i < deviceCount; ++i) {
        const auto& device = audioDevices[i];

        auto capture = std::make_unique<AudioDeviceCapture>(captureId);
        HRESULT hr = capture->StartCaptureAsync(device.Id, device.PipeId);
        if (SUCCEEDED(hr)) {
            activeDeviceCaptures[device.PipeId] = std::move(capture);
        }
    }

    for (int i = 0; i < count; ++i) {
        DWORD pid = pids[i];

        ComPtr<ApplicationLoopbackCapture> capture = Make<ApplicationLoopbackCapture>(captureId);
        HRESULT hr = capture->StartCaptureAsync(pid, true);
        if (SUCCEEDED(hr)) {
            activeCaptures[pid] = capture;
        }
    }

    return captureId;
}

void StopCapture(int* pids, int count, NativeAudioDeviceInfo* audioDevices, int deviceCount) {
    for (int i = 0; i < deviceCount; ++i) {
        const auto& device = audioDevices[i];
        auto it = activeDeviceCaptures.find(device.PipeId);
        if (it != activeDeviceCaptures.end()) {
            it->second->StopCaptureAsync();
            activeDeviceCaptures.erase(it);
        }
    }

    for (int i = 0; i < count; ++i) {
        DWORD pid = pids[i];
        auto it = activeCaptures.find(pid);
        if (it != activeCaptures.end()) {
            it->second->StopCaptureAsync();
            activeCaptures.erase(it);
        }
    }
}
