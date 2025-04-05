#include "AudioCaptureManager.h"
#include <chrono>
#include <thread>

#include "Logger.h"

std::map<long long, std::vector<ComPtr<ApplicationLoopbackCapture>>> AudioCaptureManager::_appCaptures;
std::map<long long, std::vector<std::unique_ptr<AudioDeviceCapture>>> AudioCaptureManager::_deviceCaptures;

long long AudioCaptureManager::StartCapture(const AudioDeviceInfo* inputDevices, int inputCount, const AudioDeviceInfo* outputDevices, int outputCount, const AudioSessionInfo* sessions, int sessionCount) {
    Logger::GetInstance().Log("StartCapture called.");
    Logger::GetInstance().Log(
        "Parameters: inputCount = " + std::to_string(inputCount) +
        ", outputCount = " + std::to_string(outputCount) +
        ", sessionCount = " + std::to_string(sessionCount)
    );

    auto captureId = GenerateUniqueId();
    Logger::GetInstance().Log("Generated captureId: " + std::to_string(captureId));

    for (int s = 0; s < sessionCount; ++s) {
        Logger::GetInstance().Log("Starting capture for session index " + std::to_string(s));
        const auto& session = sessions[s];

        ComPtr<ApplicationLoopbackCapture> capture = Make<ApplicationLoopbackCapture>(captureId, session.SessionIdentifier, session.ProcessId);
        if (const auto hr = capture->StartCaptureAsync(true); SUCCEEDED(hr)) {
            Logger::GetInstance().Log(
                "Successfully started ApplicationLoopbackCapture for session index " +
                std::to_string(s) + ", ProcessId = " + std::to_string(session.ProcessId)
            );
            _appCaptures[captureId].push_back(capture);
        }
        else {
            Logger::GetInstance().Log(
                "Failed to start ApplicationLoopbackCapture for session index " +
                std::to_string(s) + ", ProcessId = " + std::to_string(session.ProcessId) +
                ", HRESULT = " + std::to_string(hr)
            );
        }
    }

    for (int i = 0; i < inputCount; ++i) {
        Logger::GetInstance().Log("Starting capture for input device index " + std::to_string(i));
        const auto& device = inputDevices[i];

        auto capture = std::make_unique<AudioDeviceCapture>(captureId, device.Id, eCapture);
        if (const auto hr = capture->StartCaptureAsync(); SUCCEEDED(hr)) {
            Logger::GetInstance().Log("Successfully started AudioDeviceCapture for input device index " + std::to_string(i));
            _deviceCaptures[captureId].push_back(std::move(capture));
        }
        else {
            Logger::GetInstance().Log("Failed to start AudioDeviceCapture for input device index " + std::to_string(i) + ", HRESULT = " + std::to_string(hr));
        }
    }

    for (int i = 0; i < outputCount; ++i) {
        Logger::GetInstance().Log("Starting capture for output device index " + std::to_string(i));
        const auto& device = outputDevices[i];

        auto capture = std::make_unique<AudioDeviceCapture>(captureId, device.Id, eRender);
        if (const auto hr = capture->StartCaptureAsync(); SUCCEEDED(hr)) {
            Logger::GetInstance().Log("Successfully started AudioDeviceCapture for output device index " + std::to_string(i));
            _deviceCaptures[captureId].push_back(std::move(capture));
        }
        else {
            Logger::GetInstance().Log("Failed to start AudioDeviceCapture for output device index " + std::to_string(i) + ", HRESULT = " + std::to_string(hr));
        }
    }

    return captureId;
}

void AudioCaptureManager::StopCapture(long long captureId) {
    auto deviceIt = _deviceCaptures.find(captureId);
    if (deviceIt != _deviceCaptures.end()) {
        for (auto& capture : deviceIt->second) {
            capture->StopCaptureAsync();
        }
        _deviceCaptures.erase(deviceIt);
    }

    auto appIt = _appCaptures.find(captureId);
    if (appIt != _appCaptures.end()) {
        for (auto& capture : appIt->second) {
            capture->StopCaptureAsync();
        }
        _appCaptures.erase(appIt);
    }
}

void AudioCaptureManager::ReconnectSession(const std::wstring& sessionId, DWORD newProcessId) {
    for (auto& [captureId, capturesVector] : _appCaptures) {
        for (auto it = capturesVector.begin(); it != capturesVector.end(); ++it) {
            if ((*it)->GetSessionId() == sessionId) {
                DWORD oldProcessId = (*it)->GetProcessId();
                if (oldProcessId != newProcessId) {
                    HRESULT hr = (*it)->StopCaptureAsync();
                    if (SUCCEEDED(hr)) {
                        Logger::GetInstance().Log("Stopped session capture with pid=" + std::to_string(oldProcessId), LogLevel::Info);
                    } else {
                        Logger::GetInstance().Log("Failed to stop session capture", LogLevel::Error);
                    }

                    std::thread([it, newProcessId, sessionId]() {
                        HRESULT hr =(*it)->StartCaptureAsync(true);
                        if (SUCCEEDED(hr)) {
                            Logger::GetInstance().Log("Restarted session capture to newPid=" + std::to_string(newProcessId), LogLevel::Info);
                        }
                        else {
                            Logger::GetInstance().Log("Failed to start session capture", LogLevel::Error);
                        }
                        }).detach();
                }

                return;
            }
        }
    }
}

void AudioCaptureManager::ReconnectDevice(const std::wstring& deviceId) {
    for (auto& [captureId, capturesVector] : _deviceCaptures) {
	    for (auto it = capturesVector.begin(); it != capturesVector.end(); ++it) {
            if ((*it)->GetDeviceId() == deviceId) {
                HRESULT hr = (*it)->StopCaptureAsync();
                if (SUCCEEDED(hr)) {
                    Logger::GetInstance().Log("Stopped device capture", LogLevel::Info);
                }
                else {
                    Logger::GetInstance().Log("Failed to stop device capture", LogLevel::Error);
                }

                hr = (*it)->StartCaptureAsync();
                if (SUCCEEDED(hr)) {
                    Logger::GetInstance().Log("Restarted device capture", LogLevel::Info);
                }
                else {
                    Logger::GetInstance().Log("Failed to start device capture", LogLevel::Error);
                }
            }
	    }
    }

}

long long AudioCaptureManager::GenerateUniqueId() {
	auto now = std::chrono::system_clock::now();

	auto duration = now.time_since_epoch();
	auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();

	return millis;
}

