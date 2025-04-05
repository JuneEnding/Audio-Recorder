#include "AudioDeviceCapture.h"
#include <mfapi.h>
#include <functional>
#include <fstream>
#include <thread>
#include <limits>
#include <stdexcept>

#include "AudioDataBridge.h"
#include "AudioDeviceManagerBase.h"

#define BITS_PER_BYTE 8

AudioDeviceCapture::AudioDeviceCapture(long long captureId, std::wstring deviceId, EDataFlow dataFlow) :
	m_CaptureId(captureId),
	m_DeviceId(std::move(deviceId)),
	m_DataFlow(dataFlow) {}

AudioDeviceCapture::~AudioDeviceCapture() {
    StopCaptureAsync();
    MFShutdown();
}

HRESULT AudioDeviceCapture::InitializeCapture() {
    RETURN_IF_FAILED(m_SampleReadyEvent.create(wil::EventOptions::None));
    RETURN_IF_FAILED(MFStartup(MF_VERSION, MFSTARTUP_LITE));

    m_DeviceState = DeviceState::Initialized;

    return S_OK;
}

HRESULT AudioDeviceCapture::StartCaptureAsync() {
    RETURN_IF_FAILED(InitializeCapture());

    m_DeviceState = DeviceState::Starting;

    wil::com_ptr_nothrow<IMMDeviceEnumerator> enumerator;
    RETURN_IF_FAILED(CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_INPROC_SERVER, __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator)));

    wil::com_ptr_nothrow<IMMDevice> device;
    RETURN_IF_FAILED(enumerator->GetDevice(m_DeviceId.c_str(), &device));

    RETURN_IF_FAILED(device->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<void**>(&m_AudioClient)));

    WAVEFORMATEX* pDeviceFormat = nullptr;
    RETURN_IF_FAILED(m_AudioClient->GetMixFormat(&pDeviceFormat));

    m_CaptureFormat.wFormatTag = WAVE_FORMAT_PCM;
    m_CaptureFormat.nChannels = pDeviceFormat->nChannels;
    m_CaptureFormat.nSamplesPerSec = pDeviceFormat->nSamplesPerSec;
    m_CaptureFormat.wBitsPerSample = pDeviceFormat->wBitsPerSample;
    m_CaptureFormat.nBlockAlign = pDeviceFormat->nBlockAlign;
    m_CaptureFormat.nAvgBytesPerSec = pDeviceFormat->nAvgBytesPerSec;
    m_CaptureFormat.cbSize = 0;

    CoTaskMemFree(pDeviceFormat);

    DWORD flags = AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM;
    if (m_DataFlow == eRender) {
        flags |= AUDCLNT_STREAMFLAGS_LOOPBACK;
    }

    RETURN_IF_FAILED(m_AudioClient->Initialize(AUDCLNT_SHAREMODE_SHARED,
        flags,
        10000000,
        0,
        &m_CaptureFormat,
        nullptr));

    RETURN_IF_FAILED(m_AudioClient->GetService(IID_PPV_ARGS(&m_AudioCaptureClient)));

    RETURN_IF_FAILED(m_AudioClient->GetBufferSize(&m_BufferFrames));

    RETURN_IF_FAILED(m_AudioClient->SetEventHandle(m_SampleReadyEvent.get()));

    RETURN_IF_FAILED(m_AudioClient->Start());

    m_DeviceState = DeviceState::Capturing;

    std::thread([this]() {
        while (WaitForSingleObject(m_SampleReadyEvent.get(), INFINITE) == WAIT_OBJECT_0) {
            OnAudioSampleRequested();
        }
        }).detach();

    return S_OK;
}

HRESULT AudioDeviceCapture::StopCaptureAsync() {
    m_DeviceState = DeviceState::Stopping;

    if (m_AudioClient) {
        m_AudioClient->Stop();
        m_AudioClient->Release();
        m_AudioClient = nullptr;
    }

    m_DeviceState = DeviceState::Stopped;

    return S_OK;
}

HRESULT AudioDeviceCapture::OnAudioSampleRequested() {
    BYTE* pData = nullptr;
    UINT32 numFramesAvailable;
    DWORD dwFlags;

    if (m_DeviceState != DeviceState::Capturing) {
        return S_OK;
    }

    try {
        while (SUCCEEDED(m_AudioCaptureClient->GetNextPacketSize(&numFramesAvailable)) && numFramesAvailable > 0) {
            RETURN_IF_FAILED(m_AudioCaptureClient->GetBuffer(&pData, &numFramesAvailable, &dwFlags, nullptr, nullptr));

            DWORD dataSize = numFramesAvailable * m_CaptureFormat.nBlockAlign;

            AudioDataBridge::InvokeCallback(m_CaptureId, m_DeviceId, pData, dataSize);

            m_AudioCaptureClient->ReleaseBuffer(numFramesAvailable);
        }
    } catch (...) {
        return S_OK;
    }

    return S_OK;
}

std::wstring AudioDeviceCapture::GetDeviceId() {
    return m_DeviceId;
}
