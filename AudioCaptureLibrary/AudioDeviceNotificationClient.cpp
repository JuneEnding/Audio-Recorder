#include "AudioDeviceNotificationClient.h"
#include <optional>

#include "Logger.h"

AudioDeviceNotificationClient::AudioDeviceNotificationClient(DeviceStateChangedCallback callback, wil::com_ptr<IMMDeviceEnumerator> enumerator, EDataFlow flow)
    : _refCount(1), _stateChangedCallback(callback), _deviceEnumerator(std::move(enumerator)), _dataFlow(flow) {}

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

static std::optional<EDataFlow> GetDeviceDataFlowFromEnumerator(
    wil::com_ptr<IMMDeviceEnumerator> enumerator,
    const std::wstring& deviceId)
{
    for (EDataFlow flow : { eRender, eCapture })
    {
        wil::com_ptr<IMMDeviceCollection> collection;
        if (FAILED(enumerator->EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE | DEVICE_STATE_DISABLED | DEVICE_STATE_NOTPRESENT, &collection)))
            continue;

        UINT count = 0;
        if (FAILED(collection->GetCount(&count)))
            continue;

        for (UINT i = 0; i < count; ++i)
        {
            wil::com_ptr<IMMDevice> device;
            if (FAILED(collection->Item(i, &device)))
                continue;

            wil::unique_cotaskmem_string id;
            if (FAILED(device->GetId(&id)))
                continue;

            if (deviceId == id.get())
                return flow;
        }
    }

    return std::nullopt;
}

HRESULT STDMETHODCALLTYPE AudioDeviceNotificationClient::OnDeviceStateChanged(LPCWSTR pwstrDeviceId, DWORD dwNewState)
{
    Logger::GetInstance().Log("OnDeviceStateChanged called", LogLevel::Debug);

    if (!_stateChangedCallback) {
        Logger::GetInstance().Log("Callback is null, skipping", LogLevel::Warning);
        return S_OK;
    }

    auto flowOpt = GetDeviceDataFlowFromEnumerator(_deviceEnumerator, pwstrDeviceId);
    if (!flowOpt.has_value()) {
        Logger::GetInstance().Log("Device not found in either capture or render lists", LogLevel::Warning);
        return S_OK;
    }

    if (flowOpt.value() == _dataFlow) {
        Logger::GetInstance().Log("Device matched expected flow, calling callback", LogLevel::Info);
        _stateChangedCallback(pwstrDeviceId, static_cast<int>(dwNewState));
    }
    else {
        Logger::GetInstance().Log("Device flow does not match expected, skipping", LogLevel::Debug);
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
