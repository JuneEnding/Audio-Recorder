#include "OutputAudioDeviceManager.h"
#include "Logger.h"
#include <windows.h>
#include <psapi.h>
#include <string>
#include <filesystem>

#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "Version.lib")

OutputAudioDeviceManager::OutputAudioDeviceManager() = default;

OutputAudioDeviceManager::~OutputAudioDeviceManager() = default;

std::vector<OutputAudioDeviceInfo> OutputAudioDeviceManager::GetActiveOutputDevices() {
    std::vector<OutputAudioDeviceInfo> outputDevices;

    for (auto& device : GetActiveDevices(eRender)) {
        auto deviceInfo = GetDeviceInfo(device);
        if (!deviceInfo) {
            continue;
        }

        OutputAudioDeviceInfo outputInfo;
        outputInfo.DeviceInfo = *deviceInfo;

        auto sessionsVector = GetSessions(device);
        outputInfo.SessionCount = static_cast<int>(sessionsVector.size());
        if (outputInfo.SessionCount > 0) {
            outputInfo.Sessions = new AudioSessionInfo[outputInfo.SessionCount];
            std::copy(sessionsVector.begin(), sessionsVector.end(), outputInfo.Sessions);
        }
        else {
            outputInfo.Sessions = nullptr;
        }

        outputDevices.push_back(outputInfo);
    }

    return outputDevices;
}

std::optional<OutputAudioDeviceInfo> OutputAudioDeviceManager::GetOutputDeviceInfo(const std::wstring& deviceId) {
    auto device = GetDevice(deviceId);
    auto deviceInfo = GetDeviceInfo(device);

    if (!deviceInfo.has_value())
        return std::nullopt;

    OutputAudioDeviceInfo outputDeviceInfo;
    outputDeviceInfo.DeviceInfo = deviceInfo.value();

    auto sessionsVector = GetSessions(device);
    outputDeviceInfo.SessionCount = static_cast<int>(sessionsVector.size());
    if (outputDeviceInfo.SessionCount > 0) {
        outputDeviceInfo.Sessions = new AudioSessionInfo[outputDeviceInfo.SessionCount];
        std::copy(sessionsVector.begin(), sessionsVector.end(), outputDeviceInfo.Sessions);
    }
    else {
        outputDeviceInfo.Sessions = nullptr;
    }

    return outputDeviceInfo;
}

std::vector<AudioSessionInfo> OutputAudioDeviceManager::GetSessions(wil::com_ptr_nothrow<IMMDevice> device) {
    std::vector<AudioSessionInfo> sessions = {};

    wil::com_ptr_nothrow<IAudioSessionManager2> sessionManager;
    if (FAILED(device->Activate(__uuidof(IAudioSessionManager2), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<void**>(&sessionManager)))) {
        Logger::GetInstance().Log("Failed to activate audio session manager", LogLevel::Warning);
        return sessions;
    }

    wil::com_ptr_nothrow<IAudioSessionEnumerator> sessionEnumerator;
    if (FAILED(sessionManager->GetSessionEnumerator(&sessionEnumerator))) {
        Logger::GetInstance().Log("Failed to get session enumerator", LogLevel::Warning);
        return sessions;
    }

    int sessionCount = 0;
    if (FAILED(sessionEnumerator->GetCount(&sessionCount))) {
        Logger::GetInstance().Log("Failed to get session count", LogLevel::Warning);
        return sessions;
    }

    for (int i = 0; i < sessionCount; ++i) {
        wil::com_ptr_nothrow<IAudioSessionControl> sessionControl;
        if (FAILED(sessionEnumerator->GetSession(i, &sessionControl))) {
            Logger::GetInstance().Log("Failed to get session", LogLevel::Warning);
            continue;
        }

        wil::com_ptr_nothrow<IAudioSessionControl2> sessionControl2;
        if (FAILED(sessionControl->QueryInterface(__uuidof(IAudioSessionControl2), reinterpret_cast<void**>(&sessionControl2)))) {
            Logger::GetInstance().Log("Failed to query IAudioSessionControl2", LogLevel::Warning);
            continue;
        }

        DWORD pid = 0;
        if (FAILED(sessionControl2->GetProcessId(&pid))) {
            Logger::GetInstance().Log("Failed to get process ID", LogLevel::Warning);
            continue;
        }

        AudioSessionInfo sessionInfo = {};
        sessionInfo.PipeId = pid;

        LPWSTR displayName = nullptr;
        if (!SUCCEEDED(sessionControl2->GetDisplayName(&displayName)) && displayName != nullptr) {
            std::wstring wDisplayName(displayName);
            std::wstring finalDisplayName;

            if (wDisplayName.find(L'\\') != std::wstring::npos || wDisplayName.find(L'/') != std::wstring::npos) {
                std::filesystem::path p(wDisplayName);
                finalDisplayName = p.stem().wstring();
            }
            else {
                finalDisplayName = wDisplayName;
            }

            sessionInfo.DisplayName = SysAllocString(finalDisplayName.c_str());
            CoTaskMemFree(displayName);
        }
        else {
            std::wstring processName = GetProcessName(pid);
            sessionInfo.DisplayName = SysAllocString(processName.c_str());
        }

        LPWSTR iconPath = nullptr;
        if (SUCCEEDED(sessionControl2->GetIconPath(&iconPath))) {
            sessionInfo.IconPath = SysAllocString(iconPath);
            CoTaskMemFree(iconPath);
        }

        if (sessionControl2->IsSystemSoundsSession() == S_OK) {
            continue;
        }

        LPWSTR sessionIdentifier = nullptr;
        if (SUCCEEDED(sessionControl2->GetSessionIdentifier(&sessionIdentifier))) {
            sessionInfo.SessionIdentifier = SysAllocString(sessionIdentifier);
            CoTaskMemFree(sessionIdentifier);
        }

        LPWSTR sessionInstanceIdentifier = nullptr;
        if (SUCCEEDED(sessionControl2->GetSessionInstanceIdentifier(&sessionInstanceIdentifier))) {
            sessionInfo.SessionInstanceIdentifier = SysAllocString(sessionInstanceIdentifier);
            CoTaskMemFree(sessionInstanceIdentifier);
        }

        sessions.push_back(sessionInfo);
    }

    return sessions;
}

std::wstring OutputAudioDeviceManager::GetProcessName(DWORD processId) {
    HANDLE processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);
    if (!processHandle) {
        return L"<Unknown>";
    }

    wchar_t exePath[MAX_PATH] = { 0 };
    if (!GetModuleFileNameEx(processHandle, NULL, exePath, MAX_PATH)) {
        CloseHandle(processHandle);
        return L"<Unknown>";
    }
    CloseHandle(processHandle);

    std::wstring result = exePath;

    DWORD dummy = 0;
    DWORD infoSize = GetFileVersionInfoSize(exePath, &dummy);
    if (infoSize > 0) {
        std::vector<BYTE> versionInfo(infoSize);
        if (GetFileVersionInfo(exePath, dummy, infoSize, versionInfo.data())) {
            void* productNameBuffer = nullptr;
            UINT productNameLength = 0;
            if (VerQueryValue(versionInfo.data(), L"\\StringFileInfo\\040904b0\\ProductName", &productNameBuffer, &productNameLength)) {
                result = static_cast<wchar_t*>(productNameBuffer);
            }
        }
    }

    std::filesystem::path p(result);
    return p.stem().wstring();
}

void OutputAudioDeviceManager::RegisterSessionNotificationsForDevice(const std::wstring& deviceId, SessionStateChangedCallback callback) {
    auto device = GetDevice(deviceId);
    if (!device) {
        Logger::GetInstance().Log("Device not found for session registration", LogLevel::Warning);
        return;
    }

    wil::com_ptr<IAudioSessionManager2> sessionManager2;
    if (FAILED(device->Activate(__uuidof(IAudioSessionManager2), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<void**>(&sessionManager2)))) {
        Logger::GetInstance().Log("Failed to activate IAudioSessionManager2", LogLevel::Warning);
        return;
    }

    auto notification = new AudioSessionNotification(deviceId, callback);
    if (FAILED(sessionManager2->RegisterSessionNotification(notification))) {
        Logger::GetInstance().Log("RegisterSessionNotification failed", LogLevel::Warning);
        notification->Release();
        return;
    }

    notification->RegisterEventsForExistingSessions(sessionManager2);

    _sessionNotifications[deviceId] = notification;
}

void OutputAudioDeviceManager::UnregisterSessionNotificationsForDevice(const std::wstring& deviceId) {
    auto itNotify = _sessionNotifications.find(deviceId);
    if (itNotify != _sessionNotifications.end()) {
        itNotify->second->UnregisterAllSessionEvents();
        itNotify->second->Release();
        _sessionNotifications.erase(itNotify);
    }
}

