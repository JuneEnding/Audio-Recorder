using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using AudioRecorder.Core.Data;

namespace AudioRecorder.Core.Services;

public sealed class AudioDataProcessor
{
    private readonly ConcurrentDictionary<string, AudioData> _audioDataMap = new();
    private long? _currentCaptureId;
    private long? _lastCaptureId;

    public void StartProcessing(List<uint> processIds, List<NativeAudioDeviceInfo> audioDevices, long captureId)
    {
        _currentCaptureId = captureId;
        var audioDataList = audioDevices
            .Select(ad => new AudioData(ad, AudioTargetType.AudioDevice) { CaptureId = captureId })
            .Concat(processIds.Select(pid => new AudioData(AudioTargetType.Process) { PipeId = pid, CaptureId = captureId }));

        foreach (var audioData in audioDataList)
        {
            var pipeName = $"AudioDataPipe_{audioData.PipeId}_{_currentCaptureId}";
            Debug.WriteLine($"PipeName: {pipeName}");
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            audioData.PipeClient = client;

            if (_audioDataMap.TryAdd(pipeName, audioData))
            {
                try
                {
                    client.Connect(2000);
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Timeout while trying to connect.");
                    return;
                }

                var thread = new Thread(() => ProcessAudio(audioData, pipeName));
                audioData.ProcessingThread = thread;
                thread.Start();
            }
        }
    }

    public void StopProcessing(List<uint> pids, List<NativeAudioDeviceInfo> audioDevices)
    {
        if (_currentCaptureId == null)
            return;

        var pipeIds = audioDevices.Select(ad => ad.PipeId).Concat(pids).ToList();

        foreach (var pipeId in pipeIds)
        {
            var pipeName = $"AudioDataPipe_{pipeId}_{_currentCaptureId}";
            var audioData = _audioDataMap[pipeName];

            if (audioData.ProcessingThread != null && audioData.ProcessingThread.IsAlive)
            {
                audioData.CancelRequested = true;
                audioData.ProcessingThread.Interrupt();
                try
                {
                    audioData.ProcessingThread.Join();
                }
                catch (ThreadInterruptedException) { }
            }

            audioData.PipeClient?.Close();
        }

        _lastCaptureId = _currentCaptureId;
        _currentCaptureId = null;
    }

    private async void ProcessAudio(AudioData audioData, string pipeName)
    {
        if (audioData.PipeClient == null)
            return;

        using var reader = new BinaryReader(audioData.PipeClient);
        var buffer = new byte[4096];

        try
        {
            while (audioData.PipeClient.IsConnected && !audioData.CancelRequested)
            {
                var bytesRead = await ReadFromPipeWithTimeoutAsync(reader, buffer, 2000);
                if (bytesRead > 0)
                {
                    var data = buffer.Take(bytesRead).ToArray();
                    audioData.Buffer.AddRange(data);
                    double volume = CalculateVolumeLevel(data);
                    UpdateVolumeDisplay(pipeName, volume);
                }
            }
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("Stream closed.");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"An IO exception occured: {ex}");
        }
    }

    private async Task<int> ReadFromPipeWithTimeoutAsync(BinaryReader reader, byte[] buffer, int timeoutMilliseconds)
    {
        var readTask = Task.Run(() => reader.Read(buffer, 0, buffer.Length));
        var completedTask = await Task.WhenAny(readTask, Task.Delay(timeoutMilliseconds));

        if (completedTask == readTask)
            return await readTask;

        return 0;
    }

    public void SaveAudioData(AudioData audioData, string directoryName)
    {
        Directory.CreateDirectory(directoryName);

        string fileName = Path.Combine(directoryName, $"AudioData_{audioData.PipeId}_{audioData.CaptureId}.wav");
        byte[] wavData = ConvertToWav(audioData.Buffer.ToArray(), sampleRate: (int)audioData.SampleRate, bitsPerSample: audioData.BitsPerSample, channels: audioData.Channels);

        File.WriteAllBytes(fileName, wavData);
    }

    private byte[] ConvertToWav(byte[] rawAudioBytes, int sampleRate = 44100, int bitsPerSample = 16, int channels = 2)
    {
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            int blockAlign = channels * bitsPerSample / 8;
            int averageBytesPerSecond = sampleRate * blockAlign;

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

    public void SaveLastAudioData(string directoryName)
    {
        if (_lastCaptureId == null)
            return;

        foreach (var entry in _audioDataMap.Values)
        {
            if (entry.CaptureId == _lastCaptureId)
            {
                SaveAudioData(entry, directoryName);
            }
        }
    }

    private double CalculateVolumeLevel(byte[] audioData)
    {
        return Math.Sqrt(audioData.Select(x => x * x).Average());
    }

    private void UpdateVolumeDisplay(string pipeName, double volume)
    {
        Debug.WriteLine($"Pipe {pipeName}: Current volume level: {volume}");
    }
}