#pragma once

#include "ApplicationLoopbackCapture.h"
#include "AudioDeviceCapture.h"
#include <map>
#include <memory>
#include <vector>
#include <string>
#include "AudioDeviceInfo.h"

class AudioCaptureManager
{
public:
    static long long StartCapture(const AudioDeviceInfo* inputDevices, int inputCount,
        const AudioDeviceInfo* outputDevices, int outputCount,
        const AudioSessionInfo* sessions, int sessionCount);
    static void StopCapture(long long captureId);
    static void ReconnectSession(const std::wstring& sessionId, DWORD newProcessId);
    static void ReconnectDevice(const std::wstring& deviceId);

    AudioCaptureManager() = delete;
    ~AudioCaptureManager() = delete;
    AudioCaptureManager(const AudioCaptureManager&) = delete;
    AudioCaptureManager& operator=(const AudioCaptureManager&) = delete;

private:
    static std::map<long long, std::vector<ComPtr<ApplicationLoopbackCapture>>> _appCaptures;
    static std::map<long long, std::vector<std::unique_ptr<AudioDeviceCapture>>> _deviceCaptures;

    static long long GenerateUniqueId();
};
