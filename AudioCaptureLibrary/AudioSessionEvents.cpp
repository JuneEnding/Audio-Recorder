#include "AudioSessionEvents.h"

#include "Logger.h"

AudioSessionEvents::AudioSessionEvents(std::wstring deviceId, std::wstring sessionId, wil::com_ptr<IAudioSessionControl2> sessionControl2, SessionStateChangedCallback callback)
    : _sessionControl2(std::move(sessionControl2)), _refCount(1), _deviceId(std::move(deviceId)), _sessionId(std::move(sessionId)), _stateChangedCallback(callback) { }

STDMETHODIMP_(ULONG) AudioSessionEvents::AddRef() {
    return InterlockedIncrement(&_refCount);
}

STDMETHODIMP_(ULONG) AudioSessionEvents::Release() {
    ULONG ulRef = InterlockedDecrement(&_refCount);
    if (ulRef == 0) {
        delete this;
    }
    return ulRef;
}

STDMETHODIMP AudioSessionEvents::QueryInterface(REFIID riid, void** ppvObject) {
    if (riid == IID_IUnknown || riid == __uuidof(IAudioSessionEvents)) {
        *ppvObject = static_cast<IAudioSessionEvents*>(this);
        AddRef();
        return S_OK;
    }
    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

HRESULT STDMETHODCALLTYPE AudioSessionEvents::OnSessionDisconnected(AudioSessionDisconnectReason reason) {
    Logger::GetInstance().Log("Session disconnected", LogLevel::Info);

    if (_sessionControl2 && _sessionControl2->IsSystemSoundsSession() == S_OK) {
        Logger::GetInstance().Log("Skipping system sounds session", LogLevel::Debug);
        return S_OK;
    }

    DWORD pid = 0;
    if (_sessionControl2 && (FAILED(_sessionControl2->GetProcessId(&pid)) || pid == 0)) {
        Logger::GetInstance().Log("Skipping session with no process", LogLevel::Debug);
        return S_OK;
    }

    if (_stateChangedCallback) {
        _stateChangedCallback(_deviceId.c_str(), _sessionId.c_str(), AudioSessionStateExpired);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioSessionEvents::OnStateChanged(AudioSessionState state) {
    Logger::GetInstance().Log("Session state changed", LogLevel::Info);

    if (_sessionControl2 && _sessionControl2->IsSystemSoundsSession() == S_OK) {
        Logger::GetInstance().Log("Skipping system sounds session", LogLevel::Debug);
        return S_OK;
    }

    DWORD pid = 0;
    if (_sessionControl2 && (FAILED(_sessionControl2->GetProcessId(&pid)) || pid == 0)) {
        Logger::GetInstance().Log("Skipping session with no process", LogLevel::Debug);
        return S_OK;
    }

    if (_stateChangedCallback) {
        _stateChangedCallback(_deviceId.c_str(), _sessionId.c_str(), state);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioSessionEvents::OnDisplayNameChanged(LPCWSTR, LPCGUID) {
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioSessionEvents::OnIconPathChanged(LPCWSTR, LPCGUID) {
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioSessionEvents::OnSimpleVolumeChanged(float, BOOL, LPCGUID) {
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioSessionEvents::OnChannelVolumeChanged(DWORD, float*, DWORD, LPCGUID) {
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioSessionEvents::OnGroupingParamChanged(LPCGUID, LPCGUID) {
    return S_OK;
}

void AudioSessionEvents::Unregister() {
    if (_sessionControl2) {
        _sessionControl2->UnregisterAudioSessionNotification(this);
    }
}
