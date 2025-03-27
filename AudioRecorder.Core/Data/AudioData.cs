namespace AudioRecorder.Core.Data;

internal enum AudioTargetType
{
    Process = 0,
    AudioDevice
}

internal sealed class AudioData
{
    private readonly bool _isInstantReplayMode;
    private readonly Queue<byte> _instantReplayBuffer = new();
    private readonly List<byte> _standardBuffer = new();
    private readonly object _bufferLock = new();

    private int _instantReplayBufferSize;

    public string SourceId { get; init; }
    public long CaptureId { get; init; }
    public uint SampleRate { get; } = 44100;
    public ushort BitsPerSample { get; } = 16;
    public ushort Channels { get; } = 2;
    public Thread? ProcessingThread { get; set; }
    public bool CancelRequested { get; set; }
    public string Name { get; init; }
    public AudioTargetType Type { get; }
    public byte[] Buffer
    {
        get
        {
            if (_isInstantReplayMode)
                lock (_bufferLock)
                {
                    return _instantReplayBuffer.ToArray();
                }
            return _standardBuffer.ToArray();
        }
    }

    public AudioData(long captureId, string sourceId, string name, AudioTargetType type,
        bool isInstantReplayMode = false, int replayDurationSeconds = 0)
    {
        CaptureId = captureId;
        SourceId = sourceId;
        Name = name;
        Type = type;
        _isInstantReplayMode = isInstantReplayMode;

        if (_isInstantReplayMode)
            SetInstantReplayBufferSize(replayDurationSeconds);
    }

    public AudioData(AudioDeviceInfo deviceInfo, long captureId, AudioTargetType type,
        bool isInstantReplayMode = false, int replayDurationSeconds = 0) : this(captureId, deviceInfo.Id,
        deviceInfo.Name, type, isInstantReplayMode, replayDurationSeconds)
    {
        SampleRate = deviceInfo.SampleRate;
        BitsPerSample = deviceInfo.BitsPerSample;
        Channels = deviceInfo.Channels;
    }

    public AudioData(AudioSessionInfo sessionInfo, long captureId, AudioTargetType type,
        bool isInstantReplayMode = false, int replayDurationSeconds = 0) : this(captureId,
        sessionInfo.SessionIdentifier, sessionInfo.DisplayName, type, isInstantReplayMode, replayDurationSeconds) { }

    public void AddData(IEnumerable<byte> data)
    {
        if (_isInstantReplayMode)
        {
            lock (_bufferLock)
            {
                foreach (var dataByte in data)
                {
                    _instantReplayBuffer.Enqueue(dataByte);
                    if (_instantReplayBuffer.Count > _instantReplayBufferSize)
                        _instantReplayBuffer.Dequeue();
                }
            }
        }
        else
        {
            _standardBuffer.AddRange(data);
        }
    }

    public void ClearBuffer()
    {
        if (_isInstantReplayMode)
        {
            lock (_bufferLock)
            {
                _instantReplayBuffer.Clear();
            }
        }
        else
        {
            _standardBuffer.Clear();
        }
    }

    public void SetInstantReplayBufferSize(int durationSeconds)
    {
        if (!_isInstantReplayMode)
            throw new InvalidOperationException("Instant replay buffer size can only be set in instant replay mode.");

        var bytesPerSecond = (int)(SampleRate * Channels * BitsPerSample / 8);
        _instantReplayBufferSize = durationSeconds * bytesPerSecond;
    }
}
