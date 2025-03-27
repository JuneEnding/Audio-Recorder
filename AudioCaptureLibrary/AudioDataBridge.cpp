#include "AudioDataBridge.h"

AudioDataCallback AudioDataBridge::_callback = nullptr;

void AudioDataBridge::SetCallback(AudioDataCallback callback) {
	_callback = callback;
}

void AudioDataBridge::InvokeCallback(UINT64 captureId, const std::wstring& sourceId, const BYTE* data, DWORD length) {
	if (_callback) {
		_callback(captureId, sourceId.c_str(), data, length);
	}
}

extern "C" __declspec(dllexport) void set_audio_data_callback(AudioDataCallback callback) {
	AudioDataBridge::SetCallback(callback);
}

extern "C" __declspec(dllexport) void clear_audio_data_callback() {
	AudioDataBridge::SetCallback(nullptr);
}
