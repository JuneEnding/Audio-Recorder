#pragma once
#include <intsafe.h>
#include <string>

typedef void (*AudioDataCallback)(UINT64 captureId, const wchar_t* sourceId, const BYTE* data, DWORD length);

class AudioDataBridge {
public:
	static void SetCallback(AudioDataCallback callback);
	static void InvokeCallback(UINT64 captureId, const std::wstring& sourceId, const BYTE* data, DWORD length);

private:
	static AudioDataCallback _callback;
};