// ApplicationLoopback.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <wrl.h>
#include <iostream>
#include <map>
#include <string>
#include <chrono>
#include <sstream>
#include <iomanip>
#include <memory>

#include "pch.h"
#include "LoopbackCapture.h"


extern "C" {
    __declspec(dllexport) long long StartCapture(int *pids, int count);
    __declspec(dllexport) void StopCapture(int *pids, int count);
}

std::map<DWORD, ComPtr<CLoopbackCapture>> activeCaptures;

long long GenerateUniqueId() {
    auto now = std::chrono::system_clock::now();

    auto duration = now.time_since_epoch();
    auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();

    return millis;
}

long long StartCapture(int* pids, int count) {
    auto captureId = GenerateUniqueId();

    for (int i = 0; i < count; ++i) {
        DWORD pid = pids[i];

        ComPtr<CLoopbackCapture> capture = Make<CLoopbackCapture>(captureId);
        HRESULT hr = capture->StartCaptureAsync(pid, true);
        if (SUCCEEDED(hr)) {
            activeCaptures[pid] = capture;
        }
    }

    return captureId;
}

void StopCapture(int* pids, int count) {
    for (int i = 0; i < count; ++i) {
        DWORD pid = pids[i];
        auto it = activeCaptures.find(pid);
        if (it != activeCaptures.end()) {
            it->second->StopCaptureAsync();
            activeCaptures.erase(it);
        }
    }
}
