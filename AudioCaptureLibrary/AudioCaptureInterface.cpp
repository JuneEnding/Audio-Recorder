// AudioCaptureInterface.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <wrl.h>
#include <iostream>
#include <map>
#include <string>
#include <chrono>
#include <memory>
#include <comutil.h>
#include <vector>
#include <winapifamily.h>
#include <functional>

#include "pch.h"
#include "ApplicationLoopbackCapture.h"
#include "AudioCaptureManager.h"
#include "AudioDeviceCapture.h"
#include "Logger.h"
#include "OutputAudioDeviceManager.h"
#include "InputAudioDeviceManager.h"

static InputAudioDeviceManager* inputDeviceManager = new InputAudioDeviceManager();
static OutputAudioDeviceManager* outputDeviceManager = new OutputAudioDeviceManager();

extern "C" __declspec(dllexport) void __stdcall RegisterInputNotificationCallback(DeviceStateChangedCallback callback) {
    Logger::GetInstance().Log("RegisterInputNotificationCallback", LogLevel::Info);
    inputDeviceManager->RegisterNotificationCallback(callback, eCapture);
}

extern "C" __declspec(dllexport) void __stdcall UnregisterInputNotificationCallback() {
    Logger::GetInstance().Log("UnregisterInputNotificationCallback", LogLevel::Info);
    inputDeviceManager->UnregisterNotificationCallback();
}

extern "C" __declspec(dllexport) void __stdcall GetActiveInputAudioDevices(InputAudioDeviceInfo** devices, int* count) {
    Logger::GetInstance().Log("GetActiveInputAudioDevices", LogLevel::Info);
    if (!devices || !count)
        return;

    auto activeDevices = inputDeviceManager->GetActiveInputDevices();
    *count = static_cast<int>(activeDevices.size());
    Logger::GetInstance().Log("Devices count: " + std::to_string(*count), LogLevel::Info);

    *devices = new InputAudioDeviceInfo[*count];
    std::copy(activeDevices.begin(), activeDevices.end(), *devices);
}

extern "C" __declspec(dllexport) void __stdcall FreeInputAudioDevicesArray(InputAudioDeviceInfo* devices, int count) {
    Logger::GetInstance().Log("FreeInputAudioDevicesArray", LogLevel::Info);

    if (devices) {
        for (int i = 0; i < count; ++i) {
            if (devices[i].DeviceInfo.Id) {
                SysFreeString(devices[i].DeviceInfo.Id);
            }
            if (devices[i].DeviceInfo.Name) {
                SysFreeString(devices[i].DeviceInfo.Name);
            }
        }
        delete[] devices;
    }
}

extern "C" __declspec(dllexport) InputAudioDeviceInfo* __stdcall GetInputAudioDeviceInfo(const wchar_t* deviceId) {
    Logger::GetInstance().Log("GetInputAudioDeviceInfo", LogLevel::Info);
    if (!deviceId) {
        Logger::GetInstance().Log("Device ID is null", LogLevel::Warning);
        return nullptr;
    }

    std::wstring deviceIdStr(deviceId);
    auto deviceInfoOpt = inputDeviceManager->GetInputDeviceInfo(deviceIdStr);

    if (!deviceInfoOpt.has_value()) {
        Logger::GetInstance().Log("Failed to get device info for ID: " + std::string(deviceIdStr.begin(), deviceIdStr.end()), LogLevel::Error);
        return nullptr;
    }

    auto pDeviceInfo = new InputAudioDeviceInfo;
    *pDeviceInfo = deviceInfoOpt.value();
    return pDeviceInfo;
}

extern "C" __declspec(dllexport) void __stdcall FreeInputAudioDevice(InputAudioDeviceInfo* device) {
    if (device != nullptr) {
        if (device->DeviceInfo.Id) {
            SysFreeString(device->DeviceInfo.Id);
        }
        if (device->DeviceInfo.Name) {
            SysFreeString(device->DeviceInfo.Name);
        }
        delete device;
    }
}

extern "C" __declspec(dllexport) void __stdcall RegisterOutputNotificationCallback(DeviceStateChangedCallback callback) {
    Logger::GetInstance().Log("RegisterOutputNotificationCallback", LogLevel::Info);
    outputDeviceManager->RegisterNotificationCallback(callback, eRender);
}

extern "C" __declspec(dllexport) void __stdcall UnregisterOutputNotificationCallback() {
    Logger::GetInstance().Log("UnregisterOutputNotificationCallback", LogLevel::Info);
    outputDeviceManager->UnregisterNotificationCallback();
}

extern "C" __declspec(dllexport) void __stdcall RegisterSessionNotificationCallback(const wchar_t* deviceId, SessionStateChangedCallback callback) {
    Logger::GetInstance().Log("RegisterSessionNotificationCallback", LogLevel::Info);
    if (!deviceId || !callback) return;

    outputDeviceManager->RegisterSessionNotificationsForDevice(deviceId, callback);
}

extern "C" __declspec(dllexport) void __stdcall UnregisterSessionNotificationCallback(const wchar_t* deviceId) {
    Logger::GetInstance().Log("UnregisterSessionNotificationCallback", LogLevel::Info);
    if (!deviceId) return;

    std::wstring id(deviceId);
    outputDeviceManager->UnregisterSessionNotificationsForDevice(id);
}

static void CleanupOutputAudioDeviceInfo(OutputAudioDeviceInfo* pDeviceInfo)
{
    if (!pDeviceInfo) return;

    if (pDeviceInfo->Sessions) {
        for (int i = 0; i < pDeviceInfo->SessionCount; i++) {
            AudioSessionInfo& s = pDeviceInfo->Sessions[i];

            if (s.DisplayName) {
                SysFreeString(s.DisplayName);
                s.DisplayName = nullptr;
            }
            if (s.IconPath) {
                SysFreeString(s.IconPath);
                s.IconPath = nullptr;
            }
            if (s.SessionIdentifier) {
                SysFreeString(s.SessionIdentifier);
                s.SessionIdentifier = nullptr;
            }
            if (s.SessionInstanceIdentifier) {
                SysFreeString(s.SessionInstanceIdentifier);
                s.SessionInstanceIdentifier = nullptr;
            }
        }
        delete[] pDeviceInfo->Sessions;
        pDeviceInfo->Sessions = nullptr;
    }

    if (pDeviceInfo->DeviceInfo.Id) {
        SysFreeString(pDeviceInfo->DeviceInfo.Id);
        pDeviceInfo->DeviceInfo.Id = nullptr;
    }
    if (pDeviceInfo->DeviceInfo.Name) {
        SysFreeString(pDeviceInfo->DeviceInfo.Name);
        pDeviceInfo->DeviceInfo.Name = nullptr;
    }
}

extern "C" __declspec(dllexport) OutputAudioDeviceInfo* __stdcall GetOutputAudioDeviceInfo(const wchar_t* deviceId) {
    Logger::GetInstance().Log("GetOutputAudioDeviceInfo", LogLevel::Info);
    if (!deviceId) {
        Logger::GetInstance().Log("Device ID is null", LogLevel::Warning);
        return nullptr;
    }

    std::wstring deviceIdStr(deviceId);
    auto deviceInfoOpt = outputDeviceManager->GetOutputDeviceInfo(deviceIdStr);

    if (!deviceInfoOpt.has_value()) {
        Logger::GetInstance().Log("Failed to get device info for ID: " + std::string(deviceIdStr.begin(), deviceIdStr.end()), LogLevel::Error);
        return nullptr;
    }

    auto pDeviceInfo = new OutputAudioDeviceInfo(std::move(deviceInfoOpt.value()));
    return pDeviceInfo;
}

extern "C" __declspec(dllexport) void __stdcall FreeOutputAudioDevice(OutputAudioDeviceInfo* device) {
    if (!device)
        return;

    CleanupOutputAudioDeviceInfo(device);

    delete device;
}

extern "C" __declspec(dllexport) void __stdcall GetActiveOutputAudioDevices(OutputAudioDeviceInfo** devices, int* count) {
    Logger::GetInstance().Log("GetActiveOutputAudioDevices", LogLevel::Info);
    if (!devices || !count)
        return;

    auto activeDevices = outputDeviceManager->GetActiveOutputDevices();
    *count = static_cast<int>(activeDevices.size());
    Logger::GetInstance().Log("Devices count: " + std::to_string(*count), LogLevel::Info);

    *devices = new OutputAudioDeviceInfo[*count];
    std::copy(activeDevices.begin(), activeDevices.end(), *devices);
}

extern "C" __declspec(dllexport) void __stdcall FreeOutputAudioDevicesArray(OutputAudioDeviceInfo* devices, int count) {
    Logger::GetInstance().Log("FreeOutputAudioDevicesArray", LogLevel::Info);

    if (devices) {
        for (int i = 0; i < count; ++i) {
            CleanupOutputAudioDeviceInfo(&devices[i]);
        }

        delete[] devices;
    }
}

extern "C" __declspec(dllexport) long long __stdcall StartCapture(AudioDeviceInfo* inputDevices, int inputDeviceCount, AudioDeviceInfo* outputDevices, int outputDeviceCount, AudioSessionInfo* sessions, int sessionCount) {
    return AudioCaptureManager::StartCapture(inputDevices, inputDeviceCount, outputDevices, outputDeviceCount, sessions, sessionCount);
}

extern "C" __declspec(dllexport) void __stdcall StopCapture(long long captureId) {
    AudioCaptureManager::StopCapture(captureId);
}
