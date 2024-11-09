using AudioRecorder.Services;
using Avalonia.Controls.Templates;
using ReactiveUI;

namespace AudioRecorder.Models
{
    public class AudioDeviceInfo : ReactiveObject
    {
        public uint PipeId { get; }
        public string Id { get; }
        public uint SampleRate { get; }
        public ushort BitsPerSample { get; }
        public ushort Channels { get; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set => this.RaiseAndSetIfChanged(ref _isChecked, value);
        }

        public AudioDeviceInfo(AudioCapture.NativeAudioDeviceInfo deviceInfo)
        {
            PipeId = deviceInfo.PipeId;
            Id = deviceInfo.Id;
            Name = string.IsNullOrEmpty(deviceInfo.Name) ? "Unknown" : deviceInfo.Name;
            SampleRate = deviceInfo.SampleRate;
            BitsPerSample = deviceInfo.BitsPerSample;
            Channels = deviceInfo.Channels;
        }

        public AudioCapture.NativeAudioDeviceInfo ToNativeAudioDeviceInfo() =>
            new()
            {
                PipeId = PipeId,
                Id = Id,
                Name = Name,
                SampleRate = SampleRate,
                BitsPerSample = BitsPerSample,
                Channels = Channels
            };
    }
}
