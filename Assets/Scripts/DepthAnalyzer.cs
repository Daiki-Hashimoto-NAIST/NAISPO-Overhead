using System;

namespace Naispo.Overhead
{
    /// <summary>An inferred foreground axis, not a skeleton or identified left/right hands.</summary>
    public struct AxisResult
    {
        public bool Valid;
        // Pixel coordinates: X increases rightward, Y increases downward.
        public float CenterX;
        public float CenterY;
        // Unoriented image-space angle in [-90, 90); opposite directions are equivalent.
        public float AngleDegrees;
        // PCA anisotropy in [0, 1]. A circle has zero confidence; a thin line approaches one.
        public float Confidence;
        public int PixelCount;
    }

    /// <summary>
    /// Calibrates an empty table/floor, extracts objects above that background, and estimates
    /// the principal axis of the largest connected component. The camera must remain fixed.
    /// </summary>
    public sealed class DepthAnalyzer
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public ushort[] Background { get; private set; } = Array.Empty<ushort>();
        public byte[] Mask { get; private set; } = Array.Empty<byte>();
        public bool Calibrated { get; private set; }
        public int CalibrationFramesRemaining { get; private set; } = 30;
        public AxisResult Result { get; private set; }

        private int calibrationFrameCount = 30;
        private long[] depthSums = Array.Empty<long>();
        private int[] validCounts = Array.Empty<int>();
        private int[] componentTags = Array.Empty<int>();
        private int[] queue = Array.Empty<int>();

        public void BeginCalibration(int frames = 30)
        {
            if (frames < 1)
                throw new ArgumentOutOfRangeException(nameof(frames), "At least one calibration frame is required.");

            calibrationFrameCount = frames;
            CalibrationFramesRemaining = frames;
            Calibrated = false;
            Result = default;
            Array.Clear(Background, 0, Background.Length);
            Array.Clear(Mask, 0, Mask.Length);
            Array.Clear(depthSums, 0, depthSums.Length);
            Array.Clear(validCounts, 0, validCounts.Length);
        }

        public void Process(ushort[] depth, int width, int height, float minHeightMm, float maxHeightMm)
        {
            if (depth == null)
                throw new ArgumentNullException(nameof(depth));
            if (width <= 0 || height <= 0 || (long)width * height != depth.Length)
                throw new ArgumentException("Depth dimensions must be positive and match the input array.");
            if (float.IsNaN(minHeightMm) || float.IsInfinity(minHeightMm)
                || float.IsNaN(maxHeightMm) || float.IsInfinity(maxHeightMm) || maxHeightMm < minHeightMm)
                throw new ArgumentException("Height limits must be finite and ordered from minimum to maximum.");

            if (width != Width || height != Height)
            {
                Width = width;
                Height = height;
                Background = new ushort[depth.Length];
                Mask = new byte[depth.Length];
                depthSums = new long[depth.Length];
                validCounts = new int[depth.Length];
                componentTags = new int[depth.Length];
                queue = new int[depth.Length];
                BeginCalibration(calibrationFrameCount);
            }

            Result = default;
            if (CalibrationFramesRemaining > 0)
            {
                Calibrate(depth);
                return;
            }

            if (!Calibrated)
                return;

            for (int i = 0; i < depth.Length; i++)
            {
                int elevation = Background[i] - depth[i];
                Mask[i] = (byte)(depth[i] != 0 && Background[i] != 0
                    && elevation >= minHeightMm && elevation <= maxHeightMm ? 1 : 0);
            }

            AnalyzeLargestComponent();
        }

        private void Calibrate(ushort[] depth)
        {
            for (int i = 0; i < depth.Length; i++)
            {
                if (depth[i] == 0)
                    continue;
                depthSums[i] += depth[i];
                validCounts[i]++;
            }

            CalibrationFramesRemaining--;
            if (CalibrationFramesRemaining != 0)
                return;

            bool hasValidBackground = false;
            for (int i = 0; i < depth.Length; i++)
            {
                if (validCounts[i] == 0)
                    continue;
                Background[i] = (ushort)(depthSums[i] / validCounts[i]);
                hasValidBackground = true;
            }
            // An entirely invalid capture is not a usable calibration. The caller can retry.
            Calibrated = hasValidBackground;
        }

        private void AnalyzeLargestComponent()
        {
            Array.Clear(componentTags, 0, componentTags.Length);
            int componentId = 0;
            int largestId = 0;
            int largestCount = 0;
            double bestX = 0, bestY = 0, bestXX = 0, bestXY = 0, bestYY = 0;

            for (int seed = 0; seed < Mask.Length; seed++)
            {
                if (Mask[seed] != 1)
                    continue;

                componentId++;
                int read = 0, count = 1;
                queue[0] = seed;
                Mask[seed] = 2;
                double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0, sumYY = 0;

                while (read < count)
                {
                    int index = queue[read++];
                    int x = index % Width;
                    int y = index / Width;
                    componentTags[index] = componentId;
                    sumX += x;
                    sumY += y;
                    sumXX += (double)x * x;
                    sumXY += (double)x * y;
                    sumYY += (double)y * y;

                    int left = Math.Max(0, x - 1);
                    int right = Math.Min(Width - 1, x + 1);
                    int top = Math.Max(0, y - 1);
                    int bottom = Math.Min(Height - 1, y + 1);
                    for (int ny = top; ny <= bottom; ny++)
                    {
                        int row = ny * Width;
                        for (int nx = left; nx <= right; nx++)
                        {
                            int neighbor = row + nx;
                            if (Mask[neighbor] != 1)
                                continue;
                            Mask[neighbor] = 2;
                            queue[count++] = neighbor;
                        }
                    }
                }

                if (count > largestCount)
                {
                    largestId = componentId;
                    largestCount = count;
                    bestX = sumX;
                    bestY = sumY;
                    bestXX = sumXX;
                    bestXY = sumXY;
                    bestYY = sumYY;
                }
            }

            for (int i = 0; i < Mask.Length; i++)
                Mask[i] = (byte)(largestId != 0 && componentTags[i] == largestId ? 255 : 0);

            if (largestCount == 0)
                return;

            double centerX = bestX / largestCount;
            double centerY = bestY / largestCount;
            double xx = Math.Max(0, bestXX / largestCount - centerX * centerX);
            double yy = Math.Max(0, bestYY / largestCount - centerY * centerY);
            double xy = bestXY / largestCount - centerX * centerY;
            double trace = xx + yy;
            double difference = Math.Sqrt((xx - yy) * (xx - yy) + 4 * xy * xy);
            double majorVariance = (trace + difference) * 0.5;
            float confidence = trace > 0 ? (float)Math.Min(1, difference / trace) : 0;
            float angle = (float)(0.5 * Math.Atan2(2 * xy, xx - yy) * 180 / Math.PI);
            if (angle >= 90)
                angle -= 180;

            int minimumPixels = Math.Max(24, Mask.Length / 10000);
            Result = new AxisResult
            {
                Valid = largestCount >= minimumPixels && majorVariance >= 4 && confidence >= 0.35f,
                CenterX = (float)centerX,
                CenterY = (float)centerY,
                AngleDegrees = angle,
                Confidence = confidence,
                PixelCount = largestCount
            };
        }
    }
}
