#pragma once

#include <mmdeviceapi.h>
#include <audiopolicy.h>
#include <unordered_map>

#include "AudioSessionEvents.h"
#include "Logger.h"

class AudioSessionNotification : public IAudioSessionNotification {
public:
    explicit AudioSessionNotification(std::wstring deviceId, SessionStateChangedCallback callback);

    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;
    STDMETHODIMP QueryInterface(REFIID riid, void** ppvObject) override;

    HRESULT STDMETHODCALLTYPE OnSessionCreated(IAudioSessionControl* newSession) override;

    void RegisterEventsForExistingSessions(const wil::com_ptr<IAudioSessionManager2>& sessionManager);
    void UnregisterAllSessionEvents();

private:
    LONG _refCount;
    std::wstring _deviceId;
    SessionStateChangedCallback _stateChangedCallback;
    wil::com_ptr<IAudioSessionManager2> _sessionManager2;
    std::unordered_map<std::wstring, AudioSessionEvents*> _sessionEvents;
};
