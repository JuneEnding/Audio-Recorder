using AudioRecorder.Core.Data;
using DynamicData.Binding;
using ProtoBuf;
using ReactiveUI;

namespace AudioRecorder.Core.Services;

internal sealed class AudioStateService
{
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<AudioStateService> _instance = new(() => new AudioStateService());
    public static AudioStateService Instance => _instance.Value;

    private const string FilePath = "audio_state.bin";

    private AudioStateWrapper _inMemoryCache = new();

    private Timer? _saveTimer;
    private static readonly object SyncLock = new();

    private AudioStateService()
    {
        LoadFromFile();

        SubscribeToInputService();
        SubscribeToOutputService();
    }

    public static void Initialize() => _ = Instance;

    private void LoadFromFile()
    {
        if (!File.Exists(FilePath))
        {
            _inMemoryCache = new AudioStateWrapper();
            return;
        }

        lock (SyncLock)
        {
            using var file = File.OpenRead(FilePath);
            try
            {
                _inMemoryCache = Serializer.Deserialize<AudioStateWrapper>(file) ?? new AudioStateWrapper();
            }
            catch
            {
                _inMemoryCache = new AudioStateWrapper();
            }
        }
    }

    private void ScheduleSave()
    {
        lock (SyncLock)
        {
            _saveTimer?.Dispose();
            _saveTimer = new Timer(_ => SaveToFile(), null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        }
    }

    private void SaveToFile()
    {
        lock (SyncLock)
        {
            using var file = File.Create(FilePath);
            Serializer.Serialize(file, _inMemoryCache);
        }
    }

    private void SubscribeToInputService()
    {
        var inputService = InputAudioDeviceService.Instance;

        // Подписываемся на добавление/удаление
        inputService.ActiveInputAudioDevices
            .ToObservableChangeSet()
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    switch (change.Reason)
                    {
                        case DynamicData.ListChangeReason.Add:
                            var newDevice = change.Item.Current;
                            ApplySavedStateForInputDevice(newDevice);
                            SubscribeToIsChecked(newDevice);
                            break;

                        case DynamicData.ListChangeReason.Remove:
                            // Удалён физически, НО не меняем _inMemoryCache.
                            // Устройство остаётся в списке, чтобы при возврате восстановить True
                            break;
                    }
                }
            });

        // Подписываемся на уже имеющиеся
        foreach (var device in inputService.ActiveInputAudioDevices)
        {
            ApplySavedStateForInputDevice(device);
            SubscribeToIsChecked(device);
        }
    }

    private void SubscribeToOutputService()
    {
        var outputService = OutputAudioDeviceService.Instance;

        outputService.ActiveOutputAudioDevices
            .ToObservableChangeSet()
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    switch (change.Reason)
                    {
                        case DynamicData.ListChangeReason.Add:
                            var newDevice = change.Item.Current;
                            ApplySavedStateForOutputDevice(newDevice);
                            SubscribeToIsChecked(newDevice);
                            break;

                        case DynamicData.ListChangeReason.Remove:
                            // Удалён физически, НО не меняем _inMemoryCache.
                            // Устройство остаётся в списке, чтобы при возврате восстановить True
                            break;
                    }
                }
            });

        // Подписываемся на уже имеющиеся
        foreach (var device in outputService.ActiveOutputAudioDevices)
        {
            ApplySavedStateForOutputDevice(device);
            SubscribeToIsChecked(device);
        }
    }

    private void ApplySavedStateForInputDevice(InputAudioDevice device)
    {
        var saved = _inMemoryCache.InputDevices.FirstOrDefault(d => d.Id == device.Id);
        if (saved != null)
        {
            device.IsChecked = saved.IsChecked;
        }
    }

    private void SubscribeToIsChecked(InputAudioDevice device)
    {
        device.WhenAnyValue(d => d.IsChecked).Subscribe(newValue =>
        {
            var isChanged = false;

            var match = _inMemoryCache.InputDevices.FirstOrDefault(d => d.Id == device.Id);
            if (match == null && newValue)
            {
                match = new InputAudioDevice(new InputAudioDeviceInfo
                {
                    DeviceInfo = new AudioDeviceInfo { Id = device.Id }
                })
                {
                    IsChecked = newValue
                };

                _inMemoryCache.InputDevices.Add(match);
                isChanged = true;
            }
            else if (match != null)
            {
                isChanged = isChanged || match.IsChecked != newValue;
                match.IsChecked = newValue;

                if (!newValue)
                {
                    _inMemoryCache.InputDevices.Remove(match);
                    isChanged = true;
                }
            }

            if (isChanged)
                ScheduleSave();
        });
    }

    private void ApplySavedStateForOutputDevice(OutputAudioDevice device)
    {
        var saved = _inMemoryCache.OutputDevices.FirstOrDefault(d => d.Id == device.Id);
        if (saved == null) return;

        device.IsChecked = saved.IsChecked;

        foreach (var session in device.AudioSessions)
        {
            var savedSession = saved.AudioSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
            if (savedSession != null)
            {
                session.IsChecked = savedSession.IsChecked;
            }
        }
    }

    private void SubscribeToIsChecked(OutputAudioDevice device)
    {
        device.WhenAnyValue(d => d.IsChecked).Subscribe(newValue =>
        {
            var isChanged = false;

            var match = _inMemoryCache.OutputDevices.FirstOrDefault(d => d.Id == device.Id);
            if (match == null && newValue)
            {
                match = new OutputAudioDevice(new AudioDeviceInfo { Id = device.Id }, [])
                {
                    IsChecked = newValue 
                };

                _inMemoryCache.OutputDevices.Add(match);
                isChanged = true;
            }
            else if (match != null)
            {
                isChanged = isChanged || match.IsChecked != newValue;
                match.IsChecked = newValue;

                if (!newValue && match.AudioSessions.All(s => s.IsChecked == false))
                {
                    _inMemoryCache.OutputDevices.Remove(match);
                    isChanged = true;
                }
            }

            if (isChanged)
                ScheduleSave();
        });

        device.AudioSessions
            .ToObservableChangeSet()
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    switch (change.Reason)
                    {
                        case DynamicData.ListChangeReason.Add:
                            var newSession = change.Item.Current;
                            ApplySessionState(device, newSession);
                            SubscribeToIsChecked(device, newSession);
                            break;

                        case DynamicData.ListChangeReason.Remove:
                            // Удалён физически, НО не меняем _inMemoryCache.
                            // Устройство остаётся в списке, чтобы при возврате восстановить True
                            break;
                    }
                }
            });

        // Подписываемся на уже имеющиеся
        foreach (var session in device.AudioSessions)
        {
            ApplySessionState(device, session);
            SubscribeToIsChecked(device, session);
        }
    }

    private void ApplySessionState(OutputAudioDevice device, AudioSession session)
    {
        var savedDevice = _inMemoryCache.OutputDevices.FirstOrDefault(d => d.Id == device.Id);
        if (savedDevice == null) return;

        var savedSession = savedDevice.AudioSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
        if (savedSession != null)
        {
            session.IsChecked = savedSession.IsChecked; 
        }
    }

    private void SubscribeToIsChecked(OutputAudioDevice device, AudioSession session)
    {
        session.WhenAnyValue(s => s.IsChecked).Subscribe(newValue =>
        {
            var isChanged = false;

            var deviceCache = _inMemoryCache.OutputDevices.FirstOrDefault(d => d.Id == device.Id);
            if (deviceCache == null && newValue)
            {
                deviceCache = new OutputAudioDevice(new OutputAudioDeviceInfo
                {
                    DeviceInfo = new AudioDeviceInfo { Id = device.Id }
                });

                _inMemoryCache.OutputDevices.Add(deviceCache);
                isChanged = true;
            }

            if (deviceCache != null)
            {
                var sessionCache = deviceCache.AudioSessions.FirstOrDefault(s => s.SessionId == session.SessionId);

                if (newValue)
                {
                    if (sessionCache == null)
                    {
                        sessionCache = new AudioSession(
                            new AudioDeviceInfo(),
                            new AudioSessionInfo { SessionIdentifier = session.SessionId })
                        {
                            IsChecked = newValue
                        };

                        deviceCache.AudioSessions.Add(sessionCache);
                        isChanged = true;
                    }
                }
                else
                {
                    if (sessionCache != null)
                    {
                        deviceCache.AudioSessions.Remove(sessionCache);
                        isChanged = true;
                    }

                    if (deviceCache.AudioSessions.Count == 0 && !deviceCache.IsChecked)
                    {
                        _inMemoryCache.OutputDevices.Remove(deviceCache);
                        isChanged = true;
                    }
                }
            }

            if (isChanged)
                ScheduleSave();
        });
    }

    public void CommitSelection()
    {
        var inputService = InputAudioDeviceService.Instance;
        var selectedInputs = inputService.ActiveInputAudioDevices
            .Where(d => d.IsChecked)
            .Select(d => d.Id)
            .ToHashSet();

        _inMemoryCache.InputDevices = _inMemoryCache.InputDevices
            .Where(device => selectedInputs.Contains(device.Id))
            .ToList();

        var outputService = OutputAudioDeviceService.Instance;
        var selectedOutputs = new List<OutputAudioDevice>();
        foreach (var device in outputService.ActiveOutputAudioDevices)
        {
            if (!device.IsChecked && device.AudioSessions.All(s => !s.IsChecked))
                continue;

            var deviceState = new OutputAudioDevice(new OutputAudioDeviceInfo { DeviceInfo = new AudioDeviceInfo { Id = device.Id }})
            {
                IsChecked = device.IsChecked
            };

            var selectedSessions = device.AudioSessions
                .Where(s => s.IsChecked)
                .Select(s => s.SessionId)
                .ToHashSet();

            deviceState.AudioSessions.AddRange(_inMemoryCache.OutputDevices
                .FirstOrDefault(d => d.Id == device.Id)?
                .AudioSessions.Where(s => selectedSessions.Contains(s.SessionId)) ?? []);

            selectedOutputs.Add(deviceState);
        }

        _inMemoryCache.OutputDevices = selectedOutputs;

        SaveToFile();
    }
}