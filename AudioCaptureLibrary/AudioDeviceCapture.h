#pragma once

#include <AudioClient.h>
#include <mmdeviceapi.h>
#include <wrl.h>
#include <wil/com.h>
#include <wil/result.h>
#include <mfapi.h>
#include <string>

class AudioDeviceCapture {
public:
    AudioDeviceCapture(long long captureId);
    ~AudioDeviceCapture();

    HRESULT StartCaptureAsync(const std::wstring& deviceId);
    HRESULT StopCaptureAsync();

private:
    HRESULT InitializeCapture();
    HRESULT OnAudioSampleRequested();
    BOOL CreateServerPipe(DWORD pipeId);
    void WriteToPipe(const BYTE* data, DWORD dataSize);
    void ClosePipe();

    wil::com_ptr_nothrow<IAudioClient> m_AudioClient;
    wil::com_ptr_nothrow<IAudioCaptureClient> m_AudioCaptureClient;
    WAVEFORMATEX* m_CaptureFormat{};
    UINT32 m_BufferFrames = 0;
    wil::unique_event_nothrow m_SampleReadyEvent;

    long long m_CaptureId;
    std::wstring m_DeviceId;

};
