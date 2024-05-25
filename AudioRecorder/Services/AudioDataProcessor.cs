using AudioRecorder.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;

namespace AudioRecorder.Services
{
    public class AudioDataProcessor
    {
        private ConcurrentDictionary<string, AudioData> _audioDataMap = new();
        private long? _currentCaptureId;
        private long? _lastCaptureId;

        public void StartProcessing(List<int> pids, long captureId)
        {
            _currentCaptureId = captureId;
            foreach (int pid in pids)
            {
                string pipeName = $"AudioDataPipe_{pid}_{_currentCaptureId}";
                var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
                var audioData = new AudioData
                {
                    PipeClient = client,
                    Pid = pid,
                    CaptureId = captureId
                };

                if (_audioDataMap.TryAdd(pipeName, audioData))
                {
                    try
                    {
                        client.Connect(500);
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

        public void StopProcessing(List<int> pids)
        {
            if (_currentCaptureId == null)
                return;

            foreach(int pid in pids)
            {
                string pipeName = $"AudioDataPipe_{pid}_{_currentCaptureId}";
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

        private void ProcessAudio(AudioData audioData, string pipeName)
        {
            if (audioData.PipeClient == null)
                return;

            using (var reader = new BinaryReader(audioData.PipeClient))
            {
                try
                {
                    while (audioData.PipeClient.IsConnected && !audioData.CancelRequested)
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while (!audioData.CancelRequested && (bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
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
        }

        public void SaveAudioData(AudioData audioData, string directoryName)
        {
            Directory.CreateDirectory(directoryName);

            string fileName = Path.Combine(directoryName, $"AudioData_{audioData.Pid}_{audioData.CaptureId}.wav");
            byte[] wavData = ConvertToWav(audioData.Buffer.ToArray());

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
}
