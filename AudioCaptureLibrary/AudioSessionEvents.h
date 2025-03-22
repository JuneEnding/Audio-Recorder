#pragma once

#include <mmdeviceapi.h>
#include <string>
#include <audiopolicy.h>
#include <wil/com.h>

typedef void(__stdcall* SessionStateChangedCallback)(const wchar_t* deviceId, const wchar_t* sessionId, int newState);

class AudioSessionEvents : public IAudioSessionEvents {
public:
    explicit AudioSessionEvents(std::wstring deviceId, std::wstring sessionId, wil::com_ptr<IAudioSessionControl2> sessionControl2, SessionStateChangedCallback callback);

    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;
    STDMETHODIMP QueryInterface(REFIID riid, void** ppvObject) override;

    HRESULT STDMETHODCALLTYPE OnSessionDisconnected(AudioSessionDisconnectReason reason) override;
    HRESULT STDMETHODCALLTYPE OnStateChanged(AudioSessionState state) override;
    HRESULT STDMETHODCALLTYPE OnDisplayNameChanged(LPCWSTR, LPCGUID) override;
    HRESULT STDMETHODCALLTYPE OnIconPathChanged(LPCWSTR, LPCGUID) override;
    HRESULT STDMETHODCALLTYPE OnSimpleVolumeChanged(float, BOOL, LPCGUID) override;
    HRESULT STDMETHODCALLTYPE OnChannelVolumeChanged(DWORD, float*, DWORD, LPCGUID) override;
    HRESULT STDMETHODCALLTYPE OnGroupingParamChanged(LPCGUID, LPCGUID) override;

    void Unregister();

private:
    wil::com_ptr<IAudioSessionControl2> _sessionControl2;
    LONG _refCount;
    std::wstring _deviceId;
    std::wstring _sessionId;
    SessionStateChangedCallback _stateChangedCallback;
};
