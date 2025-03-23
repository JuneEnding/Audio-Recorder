using System.IO.Pipes;
using System.Text;
using AudioRecorder.Core.Data;

namespace AudioRecorder.Core.Services;

internal sealed class AudioDataProcessor
{
    private const string PipeNameTemplate = "AudioDataPipe_{0}_{1}";
    private const string FileNameTemplate = "{0}_{1}_{2}.wav";
    private const int PipeTimeout = 2000;

    private readonly bool _isInstantReplayMode;
    private readonly int _instantReplayDuration;
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
                new AudioData(captureId, AudioTargetType.Process, isInstantReplayMode, instantReplayDuration)
                    { PipeId = session.PipeId, Name = session.DisplayName })).ToArray();
        _isInstantReplayMode = isInstantReplayMode;
        _instantReplayDuration = instantReplayDuration;
    }

    public bool Start()
    {
        var threadsToStart = new List<Thread>();

        foreach (var audioData in _audioDataList)
        {
            var pipeName = string.Format(PipeNameTemplate, audioData.PipeId, CaptureId);

            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            audioData.PipeClient = client;

            try
            {
                client.Connect(PipeTimeout);
            }
            catch (TimeoutException)
            {
                Logger.LogError("Timeout while trying to connect.");
                return false;
            }

            var thread = new Thread(() => ProcessAudio(audioData));
            audioData.ProcessingThread = thread;
            threadsToStart.Add(thread);
        }

        foreach (var thread in threadsToStart)
            thread.Start();

        return true;
    }

    public void Stop()
    {
        foreach (var audioData in _audioDataList)
        {
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
    }

    public void SetAllInstantReplayDuration(int durationSeconds)
    {
        if (!_isInstantReplayMode)
            return;

        foreach (var audioData in _audioDataList)
            audioData.SetInstantReplayBufferSize(durationSeconds);
    }

    private async void ProcessAudio(AudioData audioData)
    {
        if (audioData.PipeClient == null)
            return;

        using var reader = new BinaryReader(audioData.PipeClient);
        var buffer = new byte[4096];

        audioData.ClearBuffer();

        try
        {
            while (audioData.PipeClient.IsConnected && !audioData.CancelRequested)
            {
                var bytesRead = await ReadFromPipeWithTimeoutAsync(reader, buffer, PipeTimeout);
                if (bytesRead > 0)
                {
                    var data = buffer.Take(bytesRead).ToArray();
                    audioData.AddData(data);
                }
            }
        }
        catch (EndOfStreamException)
        {
            Logger.LogError("Stream closed.");
        }
        catch (IOException ex)
        {
            Logger.LogError($"An IO exception occured: {ex}");
        }
    }

    private async Task<int> ReadFromPipeWithTimeoutAsync(BinaryReader reader, byte[] buffer, int timeoutMilliseconds)
    {
        var readTask = Task.Run(() =>
        {
            try
            {
                return reader.Read(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException)
            {
                // ignored
            }

            return 0;
        });

        var completedTask = await Task.WhenAny(readTask, Task.Delay(timeoutMilliseconds));

        if (completedTask == readTask)
            return await readTask;

        return 0;
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