#include "Logger.h"

Logger::LogFunc Logger::logFunc = nullptr;

Logger& Logger::GetInstance() {
    static Logger instance;
    return instance;
}

void Logger::SetLogFunction(LogFunc func) {
    logFunc = func;
}

void Logger::Log(const std::string& message, LogLevel level) {
    if (logFunc) {
        logFunc(message.c_str(), static_cast<int>(level));
    }
}

extern "C" __declspec(dllexport) void set_logger(Logger::LogFunc func) {
    Logger::SetLogFunction(func);
}
