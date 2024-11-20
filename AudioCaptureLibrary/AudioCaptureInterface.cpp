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

#include "pch.h"
#include "ApplicationLoopbackCapture.h"
#include "AudioDeviceCapture.h"
#include "Logger.h"
#include "AudioDeviceManager.h"


static AudioDeviceManager* manager = new AudioDeviceManager();

extern "C" __declspec(dllexport) void __stdcall RegisterNotificationCallback(DeviceStateChangedCallback callback) {
    Logger::GetInstance().Log("RegisterNotificationCallback", LogLevel::Info);
    manager->RegisterNotificationCallback(callback);
}

extern "C" __declspec(dllexport) void __stdcall UnregisterNotificationCallback() {
    Logger::GetInstance().Log("UnregisterNotificationCallback", LogLevel::Info);
    manager->UnregisterNotificationCallback();
}

extern "C" __declspec(dllexport) void __stdcall GetActiveAudioDevices(NativeAudioDeviceInfo** devices, int* count) {
    Logger::GetInstance().Log("GetActiveAudioDevices", LogLevel::Info);
    if (!devices || !count)
        return;

    auto activeDevices = manager->GetActiveAudioDevices();
    *count = static_cast<int>(activeDevices.size());
    Logger::GetInstance().Log("Devices count: " + std::to_string(*count), LogLevel::Info);

    *devices = new NativeAudioDeviceInfo[*count];
    std::copy(activeDevices.begin(), activeDevices.end(), *devices);
}

extern "C" __declspec(dllexport) void __stdcall FreeAudioDevicesArray(NativeAudioDeviceInfo* devices, int count) {
    Logger::GetInstance().Log("FreeAudioDevicesArray", LogLevel::Info);

    if (devices) {
        for (int i = 0; i < count; ++i) {
            SysFreeString(devices[i].Name);
            SysFreeString(devices[i].Id);
        }
        delete[] devices;
    }
}

extern "C" __declspec(dllexport) NativeAudioDeviceInfo* __stdcall GetAudioDeviceInfo(const wchar_t* deviceId) {
    Logger::GetInstance().Log("GetAudioDeviceInfo", LogLevel::Info);
    if (!deviceId) {
        Logger::GetInstance().Log("Device ID is null", LogLevel::Warning);
        return nullptr;
    }

    std::wstring deviceIdStr(deviceId);
    auto deviceInfoOpt = manager->GetAudioDeviceInfo(deviceIdStr);

    if (!deviceInfoOpt.has_value()) {
        Logger::GetInstance().Log("Failed to get device info for ID: " + std::string(deviceIdStr.begin(), deviceIdStr.end()), LogLevel::Error);
        return nullptr;
    }

    NativeAudioDeviceInfo* result = new NativeAudioDeviceInfo();
    *result = deviceInfoOpt.value();
    return result;
}

extern "C" __declspec(dllexport) void __stdcall FreeAudioDevice(NativeAudioDeviceInfo* device) {
    if (device != nullptr) {
        SysFreeString(device->Id);
        SysFreeString(device->Name);
        delete device;
    }
}

std::map<long long, std::vector<ComPtr<ApplicationLoopbackCapture>>> activeAppCaptures;
std::map<long long, std::vector<std::unique_ptr<AudioDeviceCapture>>> activeDeviceCaptures;

long long GenerateUniqueId() {
    auto now = std::chrono::system_clock::now();

    auto duration = now.time_since_epoch();
    auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();

    return millis;
}

extern "C" __declspec(dllexport) long long __stdcall StartCapture(int* pids, int count, NativeAudioDeviceInfo * audioDevices, int deviceCount) {
    auto captureId = GenerateUniqueId();

    for (int i = 0; i < deviceCount; ++i) {
        const auto& device = audioDevices[i];

        auto capture = std::make_unique<AudioDeviceCapture>(captureId);
        HRESULT hr = capture->StartCaptureAsync(device.Id, device.PipeId);
        if (SUCCEEDED(hr)) {
            activeDeviceCaptures[captureId].push_back(std::move(capture));
        }
    }

    for (int i = 0; i < count; ++i) {
        DWORD pid = pids[i];

        ComPtr<ApplicationLoopbackCapture> capture = Make<ApplicationLoopbackCapture>(captureId);
        HRESULT hr = capture->StartCaptureAsync(pid, true);
        if (SUCCEEDED(hr)) {
            activeAppCaptures[captureId].push_back(capture);
        }
    }

    return captureId;
}

extern "C" __declspec(dllexport) void __stdcall StopCapture(long long captureId) {
    auto deviceIt = activeDeviceCaptures.find(captureId);
    if (deviceIt != activeDeviceCaptures.end()) {
        for (auto& capture : deviceIt->second) {
            capture->StopCaptureAsync();
        }
        activeDeviceCaptures.erase(deviceIt);
    }
    
    auto appIt = activeAppCaptures.find(captureId);
    if (appIt != activeAppCaptures.end()) {
        for (auto& capture : appIt->second) {
            capture->StopCaptureAsync();
        }
        activeAppCaptures.erase(appIt);
    }
}
