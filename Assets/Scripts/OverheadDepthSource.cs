using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Naispo.Overhead
{
    /// <summary>Optional, depth-only capture. The demo can run without connecting a Kinect.</summary>
    [DisallowMultipleComponent]
    public sealed class OverheadDepthSource : MonoBehaviour
    {
        readonly object frameGate = new object();
        Thread worker;
        volatile bool stopRequested;
        volatile string status = "Kinect stopped — demo is available";
        volatile float fps;
        ushort[] pendingDepth, consumedDepth;
        int pendingWidth, pendingHeight;
        bool hasPending;

        public string Status => status;
        public bool Running => worker != null && worker.IsAlive;
        public float Fps => fps;

        /// <summary>
        /// Returns millimetres, with zero meaning an invalid depth. The snapshot is stable until
        /// the next successful TryTake call. Process it now, or copy it if it must be retained.
        /// Three buffers are exchanged so capture never overwrites the current consumer snapshot.
        /// </summary>
        public bool TryTake(out ushort[] depth, out int width, out int height)
        {
            lock (frameGate)
            {
                if (!hasPending || stopRequested)
                {
                    depth = null;
                    width = height = 0;
                    return false;
                }
                depth = pendingDepth;
                width = pendingWidth;
                height = pendingHeight;
                pendingDepth = consumedDepth;
                consumedDepth = depth;
                hasPending = false;
                return true;
            }
        }

        // Called explicitly by the UI, never by OnEnable: demonstration is the default input.
        public void StartCapture()
        {
            if (Running) return;
            string dataPath = Application.dataPath;
            bool editor = Application.isEditor;
            lock (frameGate)
            {
                pendingDepth = consumedDepth = null;
                pendingWidth = pendingHeight = 0;
                hasPending = false;
            }
            stopRequested = false;
            fps = 0;
            status = "Starting Azure Kinect depth capture...";
            worker = new Thread(() => CaptureLoop(dataPath, editor))
            {
                IsBackground = true,
                Name = "NAISPO Overhead Depth"
            };
            worker.Start();
        }

        public void StopCapture()
        {
            stopRequested = true;
            fps = 0;
            status = "Stopping Kinect...";
            if (worker != null && worker.IsAlive && !worker.Join(1500))
            {
                // Native initialization has no cancellation API. Its owning thread will dispose
                // the device when it returns; never abort it or dispose its handle concurrently.
                status = "Stopping Kinect; waiting for the sensor to return";
            }
            else
            {
                worker = null;
                status = "Kinect stopped";
            }
            lock (frameGate) { hasPending = false; }
        }

        void OnDisable() { StopCapture(); }

        void CaptureLoop(string dataPath, bool editor)
        {
            Device device = null;
            bool camerasStarted = false;
            bool failed = false;
            try
            {
                EnsureNativeLoaded(dataPath, editor);
                if (stopRequested) return;
                if (Device.GetInstalledCount() == 0)
                    throw new InvalidOperationException("No Azure Kinect found. Check power/USB 3, then press Kinect again.");
                device = Device.Open(0);
                if (stopRequested) return;
                device.StartCameras(new DeviceConfiguration
                {
                    DepthMode = DepthMode.NFOV_Unbinned,
                    CameraFPS = FPS.FPS30,
                    ColorResolution = ColorResolution.Off,
                    SynchronizedImagesOnly = false,
                    WiredSyncMode = WiredSyncMode.Standalone
                });
                camerasStarted = true;
                if (stopRequested) return;
                status = "Kinect connected; waiting for depth (NFOV 640 x 576 / 30 FPS)";
                var timer = Stopwatch.StartNew();
                double lastDepthTime = 0, fpsTime = 0;
                int fpsFrames = 0;
                ushort[] writeDepth = null;
                while (!stopRequested)
                {
                    try
                    {
                        using (var capture = device.GetCapture(TimeSpan.FromMilliseconds(200)))
                        using (var depth = capture.Depth)
                        {
                            if (depth == null) throw new TimeoutException("Capture has no depth image.");
                            int width = depth.WidthPixels, height = depth.HeightPixels;
                            if (writeDepth == null || writeDepth.Length != width * height)
                                writeDepth = new ushort[width * height];
                            var bytes = depth.Memory.Span;
                            for (int y = 0; y < height; ++y)
                            {
                                var row = bytes.Slice(y * depth.StrideBytes, width * sizeof(ushort));
                                MemoryMarshal.Cast<byte, ushort>(row).CopyTo(writeDepth.AsSpan(y * width, width));
                            }
                            if (stopRequested) break;
                            lock (frameGate)
                            {
                                var reusable = pendingDepth;
                                pendingDepth = writeDepth;
                                writeDepth = reusable;
                                pendingWidth = width;
                                pendingHeight = height;
                                hasPending = true;
                            }
                            lastDepthTime = timer.Elapsed.TotalSeconds;
                            ++fpsFrames;
                        }
                    }
                    catch (TimeoutException) { /* A bounded wait permits a prompt stop. */ }
                    double now = timer.Elapsed.TotalSeconds;
                    if (now - lastDepthTime > 5)
                        throw new TimeoutException("No depth frames for 5 seconds. Check power/USB and close other Kinect apps, then retry.");
                    if (now - fpsTime >= 1)
                    {
                        fps = (float)(fpsFrames / (now - fpsTime));
                        fpsFrames = 0;
                        fpsTime = now;
                        status = "Kinect depth 640 x 576 | " + fps.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " FPS";
                    }
                }
            }
            catch (Exception ex)
            {
                if (!stopRequested)
                {
                    failed = true;
                    status = ex.GetType().Name + ": " + ex.Message;
                }
            }
            finally
            {
                // Dispose even when StopCameras fails, and leave the original capture error visible.
                try { if (camerasStarted) device.StopCameras(); }
                catch (Exception ex)
                {
                    if (!failed && !stopRequested) { failed = true; status = "Kinect stop error: " + ex.Message; }
                }
                try { device?.Dispose(); }
                catch (Exception ex)
                {
                    if (!failed && !stopRequested) { failed = true; status = "Kinect cleanup error: " + ex.Message; }
                }
                fps = 0;
                if (!failed) status = "Kinect stopped";
                if (stopRequested) lock (frameGate) { hasPending = false; }
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);

        static void EnsureNativeLoaded(string dataPath, bool editor)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || IntPtr.Size != 8)
                throw new PlatformNotSupportedException("Azure Kinect requires Windows x64. Use Demo on this system.");
            string folder = Path.Combine(dataPath, editor ? "Plugins/AzureKinect/x86_64" : "Plugins/x86_64");
            // Only the Sensor SDK is needed. Explicit paths avoid modifying process-wide PATH.
            // Keep native libraries loaded across Editor domain reloads, as Unity does for plugins.
            foreach (string name in new[] { "k4a.dll", "depthengine_2_0.dll" })
            {
                string path = Path.Combine(folder, name);
                if (!File.Exists(path)) throw new FileNotFoundException("Missing Azure Kinect sensor library", path);
                if (GetModuleHandle(name) == IntPtr.Zero && LoadLibraryEx(path, IntPtr.Zero, 0x8) == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not load " + path);
            }
        }
    }
}
