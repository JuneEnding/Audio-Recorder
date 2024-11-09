using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AudioRecorder.Services
{
    public static class AudioCapture
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NativeAudioDeviceInfo
        {
            public uint PipeId;
            public string Id;
            public string Name;
            public uint SampleRate;
            public ushort BitsPerSample;
            public ushort Channels;
        }

        [DllImport("ApplicationLoopbackLibrary.dll", CharSet = CharSet.Unicode)]
        private static extern long StartCapture([In] uint[] pids, int pidCount, [In] NativeAudioDeviceInfo[] audioDevices, int deviceCount);

        [DllImport("ApplicationLoopbackLibrary.dll", CharSet = CharSet.Unicode)]
        private static extern void StopCapture([In] uint[] pids, int pidCount, [In] NativeAudioDeviceInfo[] audioDevices, int deviceCount);

        [DllImport("ApplicationLoopbackLibrary.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetAudioDevices(out int deviceCount);

        [DllImport("ApplicationLoopbackLibrary.dll", CharSet = CharSet.Unicode)]
        private static extern void FreeAudioDevicesArray(IntPtr devices, int deviceCount);

        public static long StartCapture(List<uint> pids, List<NativeAudioDeviceInfo> audioDevices)
        {
            foreach (var device in audioDevices)
                Debug.WriteLine($"GetAudioDevices: ${device.PipeId}, {device.Id}, {device.Name}");

            return StartCapture(pids.ToArray(), pids.Count, audioDevices.ToArray(), audioDevices.Count);
        }

        public static void StopCapture(List<uint> pids, List<NativeAudioDeviceInfo> audioDevices)
        {
            StopCapture(pids.ToArray(), pids.Count, audioDevices.ToArray(), audioDevices.Count);
        }

        public static NativeAudioDeviceInfo[] GetAudioDevicesArray()
        {
            var deviceArrayPtr = GetAudioDevices(out var deviceCount);
            if (deviceArrayPtr == IntPtr.Zero || deviceCount == 0)
                return Array.Empty<NativeAudioDeviceInfo>();

            var devices = new NativeAudioDeviceInfo[deviceCount];
            var currentPtr = deviceArrayPtr;

            var structSize = Marshal.SizeOf(typeof(NativeAudioDeviceInfo));
            for (var i = 0; i < deviceCount; ++i)
            {
                devices[i] = Marshal.PtrToStructure<NativeAudioDeviceInfo>(currentPtr);
                currentPtr = IntPtr.Add(currentPtr, structSize);
            }

            foreach (var device in devices)
                Debug.WriteLine($"GetAudioDevices: ${device.PipeId}, {device.Id}, {device.Name}, {device.SampleRate}, {device.BitsPerSample}, {device.Channels}");

            FreeAudioDevicesArray(deviceArrayPtr, deviceCount);
            return devices;
        }
    }
}
