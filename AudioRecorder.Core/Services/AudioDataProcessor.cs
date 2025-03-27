using System.Runtime.InteropServices;
using System.Text;
using AudioRecorder.Core.Data;

namespace AudioRecorder.Core.Services;

internal sealed class AudioDataProcessor
{
    private readonly bool _isInstantReplayMode;
    private readonly AudioData[] _audioDataList;

    public long CaptureId { get; }

    public AudioDataProcessor(long captureId, IEnumerable<AudioDeviceInfo> inputDevices,
        IEnumerable<AudioDeviceInfo> outputDevices, IEnumerable<AudioSessionInfo> sessions, bool isInstantReplayMode = false,
        int instantReplayDuration = 0)
    {
        CaptureId = captureId;

        _audioDataList = inputDevices
            .Select(ad => new AudioData(ad, captureId, AudioTargetType.AudioDevice, isInstantReplayMode,
                instantReplayDuration))
            .Concat(outputDevices.Select(ad => new AudioData(ad, captureId, AudioTargetType.AudioDevice,
                isInstantReplayMode, instantReplayDuration)))
            .Concat(sessions.Select(session =>
                new AudioData(session, captureId, AudioTargetType.Process, isInstantReplayMode, instantReplayDuration)))
            .ToArray();

        _isInstantReplayMode = isInstantReplayMode;
    }

    public bool Start()
    {
        try
        {
            AudioDataInterop.SetAudioDataCallback((captureId, sourceIdPtr, dataPtr, length) =>
            {
                var sourceId = Marshal.PtrToStringUni(sourceIdPtr);
                if (sourceId == null)
                    return;

                var audioData = _audioDataList
                    .FirstOrDefault(ad => ad.CaptureId == captureId && ad.SourceId == sourceId);

                var buffer = new byte[length];
                Marshal.Copy(dataPtr, buffer, 0, length);

                audioData?.AddData(buffer);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to set audio data callback: {ex.Message}");
            return false;
        }

        return true;
    }

    public void Stop()
    {
        AudioDataInterop.UnsetAudioDataCallback();
    }

    public void SetAllInstantReplayDuration(int durationSeconds)
    {
        if (!_isInstantReplayMode)
            return;

        foreach (var audioData in _audioDataList)
            audioData.SetInstantReplayBufferSize(durationSeconds);
    }

    public void SaveAllAudioData(string directoryName)
    {
        foreach (var entry in _audioDataList)
        {
            SaveAudioData(entry, directoryName);
        }
    }

    private void SaveAudioData(AudioData audioData, string directoryName)
    {
        var audioBuffer = audioData.Buffer.ToArray();
        audioData.ClearBuffer();

        var wavData = ConvertToWav(audioBuffer, sampleRate: (int)audioData.SampleRate,
            bitsPerSample: audioData.BitsPerSample, channels: audioData.Channels);

        var now = DateTime.Now;
        var dateFolder = now.ToString("dd.MM.yyyy");
        var timeFolder = now.ToString("HH-mm-ss");
        var targetDirectory = Path.Combine(directoryName, dateFolder, timeFolder);

        if (!Directory.Exists(targetDirectory))
            Directory.CreateDirectory(targetDirectory);

        var filePath = Path.Combine(targetDirectory, $"{audioData.Name}.wav");

        var index = 1;
        while (File.Exists(filePath))
        {
            filePath = Path.Combine(targetDirectory, $"{audioData.Name}_{index}.wav");
            ++index;
        }

        File.WriteAllBytes(filePath, wavData);
    }

    private byte[] ConvertToWav(byte[] rawAudioBytes, int sampleRate = 44100, int bitsPerSample = 16, int channels = 2)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        var blockAlign = channels * bitsPerSample / 8;
        var averageBytesPerSecond = sampleRate * blockAlign;

        // RIFF header
        writer.Write(Encoding.UTF8.GetBytes("RIFF"));
        writer.Write(0); // placeholder for the RIFF chunk size
        writer.Write(Encoding.UTF8.GetBytes("WAVE"));

        // fmt sub-chunk
        writer.Write(Encoding.UTF8.GetBytes("fmt "));
        writer.Write(16); // fmt chunk size (16 for PCM)
        writer.Write((short)1); // audio format (1 = PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(averageBytesPerSecond);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // data sub-chunk
        writer.Write(Encoding.UTF8.GetBytes("data"));
        writer.Write(rawAudioBytes.Length);
        writer.Write(rawAudioBytes);

        // Fill in the RIFF chunk size
        memoryStream.Seek(4, SeekOrigin.Begin);
        writer.Write((int)(memoryStream.Length - 8));

        return memoryStream.ToArray();
    }
}