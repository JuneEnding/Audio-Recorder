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
    __declspec(dllexport) void StartCapture(int *pids, int count);
    __declspec(dllexport) void StopCapture(int *pids, int count);
}

std::map<DWORD, ComPtr<CLoopbackCapture>> activeCaptures;

std::wstring GenerateOutputFileName(DWORD pid) {
    auto now = std::chrono::system_clock::now();
    auto in_time_t = std::chrono::system_clock::to_time_t(now);
    std::tm tm;

    localtime_s(&tm, &in_time_t);

    std::wstringstream ss;
    ss << std::put_time(&tm, L"%Y%m%d%H%M%S");
    std::wstring timeStr = ss.str();

    return L"output_" + std::to_wstring(pid) + L"_" + timeStr + L".wav";
}

void StartCapture(int* pids, int count) {
    for (int i = 0; i < count; ++i) {
        DWORD pid = pids[i];

        ComPtr<CLoopbackCapture> capture = Make<CLoopbackCapture>();
        std::wstring outputFile = GenerateOutputFileName(pid);
        HRESULT hr = capture->StartCaptureAsync(pid, true, outputFile.c_str());
        if (SUCCEEDED(hr)) {
            activeCaptures[pid] = capture;
        }
    }
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
