#pragma once

#include <Windows.h>
#include <string>

enum class LogLevel {
    Info = 0,
    Warning = 1,
    Error = 2
};

class Logger {
public:
    static Logger& GetInstance();

    void Log(const std::string& message, LogLevel level = LogLevel::Info);

    using LogFunc = void(*)(const char*, int);
    static void SetLogFunction(LogFunc func);

private:
    Logger() = default;

    Logger(const Logger&) = delete;
    Logger& operator=(const Logger&) = delete;

    static LogFunc logFunc;

    HMODULE hModule = nullptr;
};
