#include <wil/com.h>
#include <mmdeviceapi.h>
#include "AudioSessionNotification.h"

#include "AudioSessionEvents.h"

AudioSessionNotification::AudioSessionNotification(std::wstring deviceId, SessionStateChangedCallback callback)
    : _refCount(1), _deviceId(std::move(deviceId)), _stateChangedCallback(callback) { }

STDMETHODIMP_(ULONG) AudioSessionNotification::AddRef() {
    return InterlockedIncrement(&_refCount);
}

STDMETHODIMP_(ULONG) AudioSessionNotification::Release() {
    ULONG ulRef = InterlockedDecrement(&_refCount);
    if (ulRef == 0) {
        delete this;
    }
    return ulRef;
}

STDMETHODIMP AudioSessionNotification::QueryInterface(REFIID riid, void** ppvObject) {
    if (riid == IID_IUnknown || riid == __uuidof(IAudioSessionNotification)) {
        *ppvObject = static_cast<IAudioSessionNotification*>(this);
        AddRef();
        return S_OK;
    }
    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

HRESULT STDMETHODCALLTYPE AudioSessionNotification::OnSessionCreated(IAudioSessionControl* newSession) {
    Logger::GetInstance().Log("New audio session created", LogLevel::Info);

    wil::com_ptr_nothrow<IAudioSessionControl2> sessionControl2;
    if (FAILED(newSession->QueryInterface(__uuidof(IAudioSessionControl2), reinterpret_cast<void**>(&sessionControl2)))) {
        Logger::GetInstance().Log("Failed to query IAudioSessionControl2", LogLevel::Warning);
        return S_FALSE;
    }

    if (sessionControl2->IsSystemSoundsSession() == S_OK) {
        Logger::GetInstance().Log("Skipping system sounds session", LogLevel::Debug);
        return S_OK;
    }

    DWORD pid = 0;
    if (FAILED(sessionControl2->GetProcessId(&pid)) || pid == 0) {
        Logger::GetInstance().Log("Skipping session with no process ID", LogLevel::Debug);
        return S_OK;
    }

    wil::unique_cotaskmem_string sid;
    if (FAILED(sessionControl2->GetSessionIdentifier(&sid))) {
        Logger::GetInstance().Log("Failed to get session identifier", LogLevel::Warning);
        return S_FALSE;
    }
    std::wstring sessionId = sid.get();

    auto events = new AudioSessionEvents(_deviceId, sessionId, sessionControl2, _stateChangedCallback);
    if (FAILED(sessionControl2->RegisterAudioSessionNotification(events))) {
        Logger::GetInstance().Log("RegisterAudioSessionNotification failed", LogLevel::Warning);
        events->Release();
        return S_OK;
    }

    _sessionEvents[sessionId] = events;

    if (_stateChangedCallback) {
        _stateChangedCallback(_deviceId.c_str(), sessionId.c_str(), AudioSessionStateActive);
    }

    return S_OK;
}

void AudioSessionNotification::RegisterEventsForExistingSessions(const wil::com_ptr<IAudioSessionManager2>& sessionManager) {
    Logger::GetInstance().Log("RegisterEventsForExistingSessions", LogLevel::Debug);
    _sessionManager2 = sessionManager;

    wil::com_ptr_nothrow<IAudioSessionEnumerator> sessionEnumerator;
    if (FAILED(sessionManager->GetSessionEnumerator(&sessionEnumerator))) {
        Logger::GetInstance().Log("Failed to get session enumerator", LogLevel::Warning);
        return;
    }

    int count = 0;
    if (FAILED(sessionEnumerator->GetCount(&count))) {
        Logger::GetInstance().Log("Failed to get session count", LogLevel::Warning);
        return;
    }

    for (int i = 0; i < count; ++i)
    {
        wil::com_ptr<IAudioSessionControl> sessionControl;
        if (FAILED(sessionEnumerator->GetSession(i, &sessionControl))) continue;

        wil::com_ptr<IAudioSessionControl2> sessionControl2;
        if (FAILED(sessionControl->QueryInterface(__uuidof(IAudioSessionControl2), reinterpret_cast<void**>(&sessionControl2)))) continue;

        if (sessionControl2->IsSystemSoundsSession() == S_OK) continue;
        DWORD pid = 0;
        if (FAILED(sessionControl2->GetProcessId(&pid)) || pid == 0) continue;

        LPWSTR sidRaw = nullptr;
        if (FAILED(sessionControl2->GetSessionIdentifier(&sidRaw))) continue;

        wil::unique_cotaskmem_string sid(sidRaw);
        std::wstring sessionId = sid.get();

        auto events = new AudioSessionEvents(_deviceId, sessionId, sessionControl2, _stateChangedCallback);
        if (SUCCEEDED(sessionControl2->RegisterAudioSessionNotification(events))) {
            _sessionEvents[sessionId] = events;
        } else {
            events->Release();
        }
    }
}

void AudioSessionNotification::UnregisterAllSessionEvents() {
    Logger::GetInstance().Log("UnregisterAllSessionEvents", LogLevel::Debug);

    for (auto& [sessionId, eventsPtr] : _sessionEvents) {
        if (eventsPtr) {
            eventsPtr->Unregister();
            //eventsPtr->Release();
        }
    }
    _sessionEvents.clear();

    if (_sessionManager2) {
        _sessionManager2->UnregisterSessionNotification(this);
        _sessionManager2.reset();
    }
}

