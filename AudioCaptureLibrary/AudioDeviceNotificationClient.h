#pragma once

#include <mmdeviceapi.h>
#include <audiopolicy.h>
#include <wil/com.h>

typedef void(__stdcall* DeviceStateChangedCallback)(const wchar_t* deviceId, int newState);

class AudioDeviceNotificationClient : public IMMNotificationClient {
public:
    explicit AudioDeviceNotificationClient(DeviceStateChangedCallback callback, wil::com_ptr<IMMDeviceEnumerator> enumerator, EDataFlow flow);

    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;
    STDMETHODIMP QueryInterface(REFIID riid, void** ppvObject) override;

    HRESULT STDMETHODCALLTYPE OnDeviceAdded(LPCWSTR pwstrDeviceId) override;
    HRESULT STDMETHODCALLTYPE OnDeviceRemoved(LPCWSTR pwstrDeviceId) override;
    HRESULT STDMETHODCALLTYPE OnDeviceStateChanged(LPCWSTR pwstrDeviceId, DWORD dwNewState) override;
    HRESULT STDMETHODCALLTYPE OnDefaultDeviceChanged(EDataFlow flow, ERole role, LPCWSTR pwstrDeviceId) override;
    HRESULT STDMETHODCALLTYPE OnPropertyValueChanged(LPCWSTR pwstrDeviceId, const PROPERTYKEY key) override;

private:
    LONG _refCount;
    DeviceStateChangedCallback _stateChangedCallback;
    EDataFlow _dataFlow;
    wil::com_ptr<IMMDeviceEnumerator> _deviceEnumerator;
};
