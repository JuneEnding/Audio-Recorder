#include "AudioDeviceCapture.h"
#include <mfapi.h>
#include <mferror.h>
#include <functiondiscoverykeys_devpkey.h>
#include <functional>
#include <fstream>
#include <thread>
#include <limits>
#include <stdexcept>

AudioDeviceCapture::AudioDeviceCapture(long long captureId) : m_CaptureId(captureId) {}

AudioDeviceCapture::~AudioDeviceCapture() {
    StopCaptureAsync();
    MFShutdown();
}

HRESULT AudioDeviceCapture::InitializeCapture() {
    RETURN_IF_FAILED(m_SampleReadyEvent.create(wil::EventOptions::None));
    RETURN_IF_FAILED(MFStartup(MF_VERSION, MFSTARTUP_LITE));

    return S_OK;
}

HRESULT AudioDeviceCapture::StartCaptureAsync(const std::wstring& deviceId, DWORD pipeId) {
    RETURN_IF_FAILED(InitializeCapture());

    wil::com_ptr_nothrow<IMMDeviceEnumerator> enumerator;
    RETURN_IF_FAILED(CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_INPROC_SERVER, __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator)));

    wil::com_ptr_nothrow<IMMDevice> device;
    RETURN_IF_FAILED(enumerator->GetDevice(deviceId.c_str(), &device));

    RETURN_IF_FAILED(device->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<void**>(&m_AudioClient)));

    RETURN_IF_FAILED(m_AudioClient->GetMixFormat(&m_CaptureFormat));

    if (m_CaptureFormat->wFormatTag == WAVE_FORMAT_EXTENSIBLE) {
        auto* waveFormatExt = reinterpret_cast<WAVEFORMATEXTENSIBLE*>(m_CaptureFormat);
        if (waveFormatExt->SubFormat != KSDATAFORMAT_SUBTYPE_PCM &&
            waveFormatExt->SubFormat != KSDATAFORMAT_SUBTYPE_IEEE_FLOAT) {
            return AUDCLNT_E_UNSUPPORTED_FORMAT;
        }
    }

    RETURN_IF_FAILED(m_AudioClient->Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_EVENTCALLBACK, 10000000, 0, m_CaptureFormat, NULL));
    RETURN_IF_FAILED(m_AudioClient->GetService(IID_PPV_ARGS(&m_AudioCaptureClient)));

    RETURN_IF_FAILED(m_AudioClient->GetBufferSize(&m_BufferFrames));

    RETURN_IF_FAILED(m_AudioClient->SetEventHandle(m_SampleReadyEvent.get()));

    RETURN_IF_WIN32_BOOL_FALSE(CreateServerPipe(pipeId));

    RETURN_IF_FAILED(m_AudioClient->Start());

    std::thread([this]() {
        while (WaitForSingleObject(m_SampleReadyEvent.get(), INFINITE) == WAIT_OBJECT_0) {
            OnAudioSampleRequested();
        }
        }).detach();

    return S_OK;
}

HRESULT AudioDeviceCapture::StopCaptureAsync() {
    if (m_AudioClient) {
        m_AudioClient->Stop();
    }

    ClosePipe();
    return S_OK;
}

HRESULT AudioDeviceCapture::OnAudioSampleRequested() {
    BYTE* pData = nullptr;
    UINT32 numFramesAvailable;
    DWORD dwFlags;

    while (SUCCEEDED(m_AudioCaptureClient->GetNextPacketSize(&numFramesAvailable)) && numFramesAvailable > 0) {
        RETURN_IF_FAILED(m_AudioCaptureClient->GetBuffer(&pData, &numFramesAvailable, &dwFlags, nullptr, nullptr));

        DWORD dataSize = numFramesAvailable * m_CaptureFormat->nBlockAlign;

        if (m_CaptureFormat->wFormatTag == WAVE_FORMAT_EXTENSIBLE &&
            reinterpret_cast<WAVEFORMATEXTENSIBLE*>(m_CaptureFormat)->SubFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT) {
            const float* floatData = reinterpret_cast<const float*>(pData);
            std::vector<int32_t> pcmData(numFramesAvailable);

            for (UINT32 i = 0; i < numFramesAvailable; ++i) {
                pcmData[i] = static_cast<int32_t>(floatData[i] * (std::numeric_limits<int32_t>::max)());
            }

            size_t dataSize = pcmData.size() * sizeof(int32_t);
            if (dataSize > static_cast<size_t>((std::numeric_limits<int32_t>::max)())) {
                throw std::overflow_error("Data size exceeds int32_t limits.");
            }

            int32_t dataSizeInt32 = static_cast<int32_t>(dataSize);

            WriteToPipe(reinterpret_cast<const BYTE*>(pcmData.data()), dataSizeInt32);
        }
        else {
            WriteToPipe(pData, dataSize);
        }

        m_AudioCaptureClient->ReleaseBuffer(numFramesAvailable);
    }
    return S_OK;
}

BOOL AudioDeviceCapture::CreateServerPipe(DWORD pipeId) {
    TCHAR pipeName[256];
    swprintf_s(pipeName, _countof(pipeName), L"\\\\.\\pipe\\AudioDataPipe_%lu_%lld", pipeId, m_CaptureId);

    m_hPipe = CreateNamedPipeW(
        pipeName,
        PIPE_ACCESS_OUTBOUND,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
        1,
        1024 * 16,
        0,
        0,
        NULL
    );

    return m_hPipe != INVALID_HANDLE_VALUE;
}

void AudioDeviceCapture::WriteToPipe(const BYTE* data, DWORD dataSize) {
    if (m_hPipe != INVALID_HANDLE_VALUE) {
        DWORD written = 0;
        WriteFile(m_hPipe, data, dataSize, &written, NULL);
    }
}

void AudioDeviceCapture::ClosePipe() {
    if (m_hPipe != NULL) {
        CloseHandle(m_hPipe);
        m_hPipe = NULL;
    }
}
