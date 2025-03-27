#pragma once

#include <AudioClient.h>
#include <mmdeviceapi.h>
#include <initguid.h>
#include <guiddef.h>
#include <mfapi.h>

#include <wrl\implements.h>
#include <wil\com.h>
#include <wil\result.h>

#include "Common.h"

using namespace Microsoft::WRL;

class ApplicationLoopbackCapture :
    public RuntimeClass< RuntimeClassFlags< ClassicCom >, FtmBase, IActivateAudioInterfaceCompletionHandler >
{
public:
    ApplicationLoopbackCapture(long long captureId) : m_CaptureId(captureId) {}
    ~ApplicationLoopbackCapture();

    HRESULT StartCaptureAsync(DWORD processId, const std::wstring& sessionId, bool includeProcessTree);
    HRESULT StopCaptureAsync();

    METHODASYNCCALLBACK(ApplicationLoopbackCapture, StartCapture, OnStartCapture);
    METHODASYNCCALLBACK(ApplicationLoopbackCapture, StopCapture, OnStopCapture);
    METHODASYNCCALLBACK(ApplicationLoopbackCapture, SampleReady, OnSampleReady);
    METHODASYNCCALLBACK(ApplicationLoopbackCapture, FinishCapture, OnFinishCapture);

    // IActivateAudioInterfaceCompletionHandler
    STDMETHOD(ActivateCompleted)(IActivateAudioInterfaceAsyncOperation* operation);

private:
    // NB: All states >= Initialized will allow some methods
        // to be called successfully on the Audio Client
    enum class DeviceState
    {
        Uninitialized,
        Error,
        Initialized,
        Starting,
        Capturing,
        Stopping,
        Stopped,
    };

    HRESULT OnStartCapture(IMFAsyncResult* pResult);
    HRESULT OnStopCapture(IMFAsyncResult* pResult);
    HRESULT OnFinishCapture(IMFAsyncResult* pResult);
    HRESULT OnSampleReady(IMFAsyncResult* pResult);

    HRESULT InitializeLoopbackCapture();
    HRESULT OnAudioSampleRequested();
    BOOL CreateServerPipe(DWORD pid);
    void WriteToPipe(const PBYTE data, DWORD dataSize);
    void ClosePipe();

    HRESULT ActivateAudioInterface(DWORD processId, bool includeProcessTree);
    HRESULT FinishCaptureAsync();

    HRESULT SetDeviceStateErrorIfFailed(HRESULT hr);

    wil::com_ptr_nothrow<IAudioClient> m_AudioClient;
    WAVEFORMATEX m_CaptureFormat{};
    UINT32 m_BufferFrames = 0;
    wil::com_ptr_nothrow<IAudioCaptureClient> m_AudioCaptureClient;
    wil::com_ptr_nothrow<IMFAsyncResult> m_SampleReadyAsyncResult;

    wil::unique_event_nothrow m_SampleReadyEvent;
    MFWORKITEM_KEY m_SampleReadyKey = 0;
    wil::critical_section m_CritSec;
    DWORD m_dwQueueID = 0;
    DWORD m_cbHeaderSize = 0;
    DWORD m_cbDataSize = 0;

    // This member are used to communicate between the main thread
    // and the ActivateCompleted callback.
    HRESULT m_activateResult = E_UNEXPECTED;

    DeviceState m_DeviceState{ DeviceState::Uninitialized };
    wil::unique_event_nothrow m_hActivateCompleted;
    wil::unique_event_nothrow m_hCaptureStopped;

    long long m_CaptureId = 0;
    std::wstring m_sessionId;
};
