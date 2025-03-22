#pragma once
#include <unordered_map>

#include "AudioDeviceManagerBase.h"
#include "AudioDeviceInfo.h"
#include "AudioSessionEvents.h"
#include "AudioSessionNotification.h"

class OutputAudioDeviceManager : public AudioDeviceManagerBase {
public:
    OutputAudioDeviceManager();
    ~OutputAudioDeviceManager();

    std::vector<OutputAudioDeviceInfo> GetActiveOutputDevices();
    std::optional<OutputAudioDeviceInfo> GetOutputDeviceInfo(const std::wstring& deviceId);
    void RegisterSessionNotificationsForDevice(const std::wstring& deviceId, SessionStateChangedCallback callback);
    void UnregisterSessionNotificationsForDevice(const std::wstring& deviceId);

private:
    std::vector<AudioSessionInfo> GetSessions(wil::com_ptr_nothrow<IMMDevice> device);
    std::wstring GetProcessName(DWORD processId);

    std::unordered_map<std::wstring, AudioSessionNotification*> _sessionNotifications;
};
