#pragma once

#include <AudioClient.h>
#include <mmdeviceapi.h>
#include <wrl.h>
#include <wil/com.h>
#include <wil/result.h>
#include <string>

#include "DeviceState.h"

class AudioDeviceCapture {
public:
    AudioDeviceCapture(long long captureId, std::wstring deviceId, EDataFlow dataFlow);
    ~AudioDeviceCapture();

    HRESULT StartCaptureAsync();
    HRESULT StopCaptureAsync();

    std::wstring GetDeviceId();

private:
    HRESULT InitializeCapture();
    HRESULT OnAudioSampleRequested();

    wil::com_ptr_nothrow<IAudioClient> m_AudioClient;
    wil::com_ptr_nothrow<IAudioCaptureClient> m_AudioCaptureClient;
    WAVEFORMATEX m_CaptureFormat{};
    UINT32 m_BufferFrames = 0;
    wil::unique_event_nothrow m_SampleReadyEvent;

    DeviceState m_DeviceState{ DeviceState::Uninitialized };

    long long m_CaptureId;
    std::wstring m_DeviceId;
    EDataFlow m_DataFlow;
};
