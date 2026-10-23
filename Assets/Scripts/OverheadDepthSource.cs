using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Naispo.Overhead
{
    /// <summary>Optional depth capture, with wrist tracking for the front-facing mode.</summary>
    [DisallowMultipleComponent]
    public sealed class OverheadDepthSource : MonoBehaviour
    {
        readonly object frameGate = new object();
        Thread worker;
        volatile bool stopRequested, starting;
        volatile string status = "Kinect stopped — demo is available";
        volatile float fps;
        ushort[] pendingDepth, consumedDepth;
        FrontPose pendingPose;
        int pendingWidth, pendingHeight;
        bool hasPending;
        CaptureOptions activeOptions, requestedOptions;

        public string Status => requestedOptions != null
            ? "Switching Kinect input; waiting for the previous capture to stop..." : status;
        public bool Running => worker != null && worker.IsAlive;
        public bool Starting => starting || requestedOptions != null;
        public bool CanStart => !Running && requestedOptions == null;
        public float Fps => fps;

        /// <summary>
        /// Returns paired depth and wrist data from one capture. Depth is in millimetres and zero
        /// means invalid. The depth array remains stable until the next successful TryTake call;
        /// copy it if retaining it longer. A missing body always produces a new invalid pose.
        /// </summary>
        public bool TryTake(out ushort[] depth, out int width, out int height, out FrontPose pose)
        {
            lock (frameGate)
            {
                if (!hasPending || stopRequested || requestedOptions != null)
                {
                    depth = null;
                    width = height = 0;
                    pose = default;
                    return false;
                }
                depth = pendingDepth;
                width = pendingWidth;
                height = pendingHeight;
                pose = pendingPose;
                pendingDepth = consumedDepth;
                consumedDepth = depth;
                hasPending = false;
                return true;
            }
        }

        public bool TryTake(out ushort[] depth, out int width, out int height)
            => TryTake(out depth, out width, out height, out _);

        // If native cleanup is still running, retain the latest request and let Update start it
        // once the old worker has completely relinquished Kinect. No automatic start on enable.
        public void StartCapture(bool trackBody = false)
        {
            var options = new CaptureOptions(Application.dataPath, Application.streamingAssetsPath,
                Application.isEditor, trackBody);
            if (Running)
            {
                if (!stopRequested && activeOptions.TrackBody == trackBody) return;
                requestedOptions = options;
                stopRequested = true;
                lock (frameGate) { hasPending = false; pendingPose = default; }
                return;
            }
            requestedOptions = null;
            BeginCapture(options);
        }

        void BeginCapture(CaptureOptions options)
        {
            lock (frameGate)
            {
                pendingDepth = consumedDepth = null;
                pendingWidth = pendingHeight = 0;
                pendingPose = default;
                hasPending = false;
            }
            activeOptions = options;
            stopRequested = false;
            starting = true;
            fps = 0;
            status = options.TrackBody ? "Starting front wrist tracking (DirectML)..." : "Starting Azure Kinect depth capture...";
            worker = new Thread(() => CaptureLoop(options))
            {
                IsBackground = true,
                Name = options.TrackBody ? "NAISPO Front Wrists" : "NAISPO Overhead Depth"
            };
            worker.Start();
        }

        void Update()
        {
            if (requestedOptions == null || Running) return;
            var next = requestedOptions;
            requestedOptions = null;
            BeginCapture(next);
        }

        public void StopCapture()
        {
            requestedOptions = null;
            stopRequested = true;
            starting = false;
            fps = 0;
            status = "Stopping Kinect...";
            if (worker != null && worker.IsAlive && !worker.Join(1500))
            {
                // Tracker.Create has no cancellation API. Never abort a native call or destroy
                // another thread's handles; cleanup will run when initialization returns.
                status = "Stopping Kinect; waiting for the sensor / tracker to return";
            }
            else
            {
                worker = null;
                status = "Kinect stopped";
            }
            lock (frameGate) { hasPending = false; pendingPose = default; }
        }

        void OnDisable() { StopCapture(); }

        void CaptureLoop(CaptureOptions options)
        {
            Device device = null;
            FrontBodyTracker bodyTracker = null;
            bool camerasStarted = false, failed = false;
            try
            {
                // Check all requested dependencies before opening/starting the sensor.
                EnsureNativeLoaded(options);
                if (stopRequested) return;
                if (Device.GetInstalledCount() == 0)
                    throw new InvalidOperationException("No Azure Kinect found. Check power/USB 3, then connect again.");
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
                if (options.TrackBody)
                {
                    status = "Sensor connected; loading front wrist tracker (DirectML)...";
                    bodyTracker = new FrontBodyTracker(device.GetCalibration(), options.ModelPath);
                    if (stopRequested) return;
                }
                starting = false;
                status = options.TrackBody ? "Front tracker ready; waiting for a person" : "Kinect connected; waiting for depth (640 x 576 / 30 FPS)";
                var timer = Stopwatch.StartNew();
                double lastCaptureTime = 0, lastResultTime = 0, fpsTime = 0;
                int fpsFrames = 0, inFlight = 0;
                ushort[] writeDepth = null;
                FrontPose latestPose = default;
                bool stalePose = false;
                // Pair SDK timestamps with this process's monotonic capture time. Receiving a
                // delayed inference result must not make old wrist coordinates appear fresh.
                var captureTimes = new Dictionary<long, double>(2);
                while (!stopRequested)
                {
                    if (bodyTracker == null)
                    {
                        try
                        {
                            using (var capture = device.GetCapture(TimeSpan.FromMilliseconds(100)))
                            using (var depth = capture.Depth)
                            {
                                if (depth != null)
                                {
                                    lastCaptureTime = timer.Elapsed.TotalSeconds;
                                    if (Publish(depth, default, ref writeDepth)) ++fpsFrames;
                                }
                            }
                        }
                        catch (TimeoutException) { /* Bounded wait; cancellation is checked each loop. */ }
                    }
                    else
                    {
                        // At most two captures in inference, and one published result for Unity.
                        // An empty skeleton result still carries matched depth and an invalid pose.
                        if (inFlight < 2)
                        {
                            try
                            {
                                using (var capture = device.GetCapture(TimeSpan.FromMilliseconds(50)))
                                using (var depth = capture.Depth)
                                {
                                    if (depth == null) throw new InvalidDataException("Sensor capture has no depth for body tracking.");
                                    lastCaptureTime = timer.Elapsed.TotalSeconds;
                                    bodyTracker.EnqueueCapture(capture, TimeSpan.FromMilliseconds(20));
                                    captureTimes[depth.DeviceTimestamp.Ticks] = lastCaptureTime;
                                    ++inFlight;
                                }
                            }
                            catch (TimeoutException) { }
                        }
                        if (stopRequested) break;
                        using (var result = bodyTracker.PopResult(TimeSpan.FromMilliseconds(20)))
                        {
                            if (result != null)
                            {
                                inFlight = Math.Max(0, inFlight - 1);
                                lastResultTime = timer.Elapsed.TotalSeconds;
                                // Never pair an older skeleton with the sensor's newest capture.
                                using (var matchedCapture = result.Capture)
                                using (var depth = matchedCapture.Depth)
                                {
                                    if (depth == null) throw new InvalidDataException("Body tracker returned a capture without depth.");
                                    long timestamp = depth.DeviceTimestamp.Ticks;
                                    bool matched = captureTimes.TryGetValue(timestamp, out double capturedAt);
                                    captureTimes.Remove(timestamp);
                                    // An unexpected result cannot be treated as fresh. Clear its
                                    // unmatched bookkeeping as well, keeping storage bounded.
                                    if (!matched) captureTimes.Clear();
                                    stalePose = !matched || timer.Elapsed.TotalSeconds - capturedAt > 0.5;
                                    latestPose = stalePose ? default : bodyTracker.ReadPose(result);
                                    if (stalePose) status = "Front tracking delayed; wrist input paused until a fresh capture arrives";
                                    if (Publish(depth, latestPose, ref writeDepth)) ++fpsFrames;
                                }
                            }
                        }
                    }
                    double now = timer.Elapsed.TotalSeconds;
                    if (inFlight == 0 && now - lastCaptureTime > 5)
                        throw new TimeoutException("No depth captures for 5 seconds. Check power/USB and close other Kinect apps, then reconnect.");
                    if (bodyTracker != null && now - lastResultTime > 15)
                        throw new TimeoutException("No body-tracking results for 15 seconds. Reconnect front mode or use overhead/demo.");
                    if (now - fpsTime >= 1)
                    {
                        fps = (float)(fpsFrames / (now - fpsTime));
                        fpsFrames = 0;
                        fpsTime = now;
                        string rate = fps.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                        status = bodyTracker == null ? "Kinect depth 640 x 576 | " + rate + " FPS"
                            : "Front DirectML | " + rate + " FPS | " + (stalePose ? "tracking delayed; wrist input paused" : latestPose.HasBody ? "body " + latestPose.BodyId
                                : bodyTracker.HasSelectedBody ? "selected person missing; reconnect to select another" : "waiting for a person");
                    }
                }
            }
            catch (Exception ex)
            {
                if (!stopRequested) { failed = true; status = ex.GetType().Name + ": " + ex.Message; }
            }
            finally
            {
                // All native resources belong to this worker. Run each cleanup independently.
                try { bodyTracker?.Dispose(); }
                catch (Exception ex)
                {
                    if (!failed && !stopRequested) { failed = true; status = "Tracker cleanup error: " + ex.Message; }
                }
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
                starting = false;
                lock (frameGate) { hasPending = false; pendingPose = default; }
                if (!failed) status = "Kinect stopped";
            }
        }

        bool Publish(Microsoft.Azure.Kinect.Sensor.Image depth, FrontPose pose, ref ushort[] writeDepth)
        {
            int width = depth.WidthPixels, height = depth.HeightPixels;
            if (writeDepth == null || writeDepth.Length != width * height)
                writeDepth = new ushort[width * height];
            var bytes = depth.Memory.Span;
            for (int y = 0; y < height; ++y)
            {
                var row = bytes.Slice(y * depth.StrideBytes, width * sizeof(ushort));
                MemoryMarshal.Cast<byte, ushort>(row).CopyTo(writeDepth.AsSpan(y * width, width));
            }
            lock (frameGate)
            {
                if (stopRequested) return false;
                var reusable = pendingDepth;
                pendingDepth = writeDepth;
                writeDepth = reusable;
                pendingWidth = width;
                pendingHeight = height;
                pendingPose = pose;
                hasPending = true;
                return true;
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);

        static void EnsureNativeLoaded(CaptureOptions options)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || IntPtr.Size != 8)
                throw new PlatformNotSupportedException("Azure Kinect requires Windows x64. Use Demo on this system.");
            string folder = Path.Combine(options.DataPath, options.Editor ? "Plugins/AzureKinect/x86_64" : "Plugins/x86_64");
            string[] libraries = options.TrackBody
                ? new[] { "k4a.dll", "depthengine_2_0.dll", "directml.dll", "onnxruntime.dll", "onnxruntime_providers_shared.dll", "k4abt.dll" }
                : new[] { "k4a.dll", "depthengine_2_0.dll" };
            foreach (string name in libraries)
            {
                string path = Path.Combine(folder, name);
                if (!File.Exists(path))
                    throw new FileNotFoundException("Missing Kinect library " + name + ". " +
                        (name == "k4a.dll" || name == "depthengine_2_0.dll"
                            ? "Restore the Sensor SDK libraries, then rebuild."
                            : "Run Setup-BodyTracking.cmd before using front mode."), path);
            }
            if (options.TrackBody)
            {
                if (!File.Exists(options.ModelPath))
                    throw new FileNotFoundException("Front tracking model is missing. Run Setup-BodyTracking.cmd, then rebuild.", options.ModelPath);
                if (new FileInfo(options.ModelPath).Length < 1024 * 1024)
                    throw new InvalidDataException("Front tracking model is incomplete (possibly a Git LFS pointer). Run Setup-BodyTracking.cmd, then rebuild.");
            }
            // Explicit paths avoid changing PATH. Native modules remain loaded across Editor
            // domain reloads. Body dependencies are never loaded in depth-only mode.
            foreach (string name in libraries)
            {
                string path = Path.Combine(folder, name);
                if (GetModuleHandle(name) == IntPtr.Zero && LoadLibraryEx(path, IntPtr.Zero, 0x8) == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not load " + path);
            }
        }

        sealed class CaptureOptions
        {
            public readonly string DataPath, ModelPath;
            public readonly bool Editor, TrackBody;
            public CaptureOptions(string dataPath, string streamingPath, bool editor, bool trackBody)
            {
                DataPath = dataPath;
                ModelPath = Path.Combine(streamingPath, "AzureKinect/dnn_model_2_0_op11.onnx");
                Editor = editor;
                TrackBody = trackBody;
            }
        }
    }
}
