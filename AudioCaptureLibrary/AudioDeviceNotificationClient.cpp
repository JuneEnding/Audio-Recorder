#include "AudioDeviceNotificationClient.h"
#include "Logger.h"

AudioDeviceNotificationClient::AudioDeviceNotificationClient(DeviceStateChangedCallback callback)
    : _refCount(1), _stateChangedCallback(callback) {}

STDMETHODIMP_(ULONG) AudioDeviceNotificationClient::AddRef() {
    return InterlockedIncrement(&_refCount);
}

STDMETHODIMP_(ULONG) AudioDeviceNotificationClient::Release() {
    ULONG ulRef = InterlockedDecrement(&_refCount);
    if (ulRef == 0) {
        delete this;
    }
    return ulRef;
}

STDMETHODIMP AudioDeviceNotificationClient::QueryInterface(REFIID riid, void** ppvObject) {
    if (riid == IID_IUnknown || riid == __uuidof(IMMNotificationClient)) {
        *ppvObject = static_cast<IMMNotificationClient*>(this);
        AddRef();
        return S_OK;
    }
    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

HRESULT STDMETHODCALLTYPE AudioDeviceNotificationClient::OnDeviceAdded(LPCWSTR pwstrDeviceId) {
    Logger::GetInstance().Log("Device Added", LogLevel::Info);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioDeviceNotificationClient::OnDeviceRemoved(LPCWSTR pwstrDeviceId) {
    Logger::GetInstance().Log("Device Removed", LogLevel::Info);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioDeviceNotificationClient::OnDeviceStateChanged(LPCWSTR pwstrDeviceId, DWORD dwNewState) {
    Logger::GetInstance().Log("Device State Changed", LogLevel::Info);
    if (_stateChangedCallback) {
        _stateChangedCallback(pwstrDeviceId, static_cast<int>(dwNewState));
    }
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioDeviceNotificationClient::OnDefaultDeviceChanged(EDataFlow flow, ERole role, LPCWSTR pwstrDefaultDeviceId) {
    Logger::GetInstance().Log("Default Device Changed", LogLevel::Info);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioDeviceNotificationClient::OnPropertyValueChanged(LPCWSTR pwstrDeviceId, const PROPERTYKEY key) {
    Logger::GetInstance().Log("Property Value Changed", LogLevel::Info);
    return S_OK;
}
