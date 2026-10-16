using System;
using NUnit.Framework;

namespace Naispo.Overhead.Tests
{
    public sealed class DepthAnalyzerTests
    {
        [Test]
        public void CalibrationAveragesValidSamplesAndIgnoresZeroDepth()
        {
            var analyzer = new DepthAnalyzer();
            analyzer.BeginCalibration(3);
            analyzer.Process(new ushort[] { 1000, 0, 0, 900 }, 2, 2, 100, 500);
            Assert.That(analyzer.CalibrationFramesRemaining, Is.EqualTo(2));
            Assert.That(analyzer.Calibrated, Is.False);
            analyzer.Process(new ushort[] { 1200, 1500, 0, 1000 }, 2, 2, 100, 500);
            analyzer.Process(new ushort[] { 0, 0, 0, 1100 }, 2, 2, 100, 500);

            Assert.That(analyzer.Calibrated, Is.True);
            Assert.That(analyzer.CalibrationFramesRemaining, Is.Zero);
            Assert.That(analyzer.Background, Is.EqualTo(new ushort[] { 1100, 1500, 0, 1000 }));
            Assert.That(analyzer.Result.Valid, Is.False);
        }

        [Test]
        public void EmptyCalibrationIsNotUsableAndDoesNotInventForeground()
        {
            var analyzer = new DepthAnalyzer();
            analyzer.BeginCalibration(1);
            analyzer.Process(new ushort[64 * 64], 64, 64, 100, 500);
            analyzer.Process(Filled(64, 64, 1000), 64, 64, 100, 500);

            Assert.That(analyzer.Calibrated, Is.False);
            Assert.That(analyzer.CalibrationFramesRemaining, Is.Zero);
            Assert.That(analyzer.Result.Valid, Is.False);
            Assert.That(analyzer.Result.PixelCount, Is.Zero);
            Assert.That(Array.FindIndex(analyzer.Mask, p => p != 0), Is.EqualTo(-1));
        }

        [Test]
        public void MissingLiveOrBackgroundDepthCannotCreateForeground()
        {
            const int size = 64;
            var background = Filled(size, size, 2000);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size / 2; x++)
                    background[y * size + x] = 0;
            var analyzer = Calibrated(background, size, size);
            var depth = Filled(size, size, 0);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size / 2; x++)
                    depth[y * size + x] = 1700;
            analyzer.Process(depth, size, size, 100, 2500);

            Assert.That(analyzer.Result.PixelCount, Is.Zero);
            Assert.That(Array.FindIndex(analyzer.Mask, p => p != 0), Is.EqualTo(-1));
        }

        [TestCase(-70f)]
        [TestCase(-35f)]
        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(75f)]
        [TestCase(90f)]
        public void RotatedElongatedForegroundProducesImageSpaceAxis(float expectedAngle)
        {
            const int size = 96;
            var analyzer = Calibrated(Filled(size, size, 2000), size, size);
            var depth = Filled(size, size, 2000);
            Rectangle(depth, size, size, 48, 48, 30, 5, expectedAngle, 1700);
            analyzer.Process(depth, size, size, 100, 500);

            Assert.That(analyzer.Result.Valid, Is.True);
            Assert.That(analyzer.Result.CenterX, Is.EqualTo(48).Within(0.5));
            Assert.That(analyzer.Result.CenterY, Is.EqualTo(48).Within(0.5));
            float difference = Math.Abs(analyzer.Result.AngleDegrees - expectedAngle);
            difference = Math.Min(difference, 180 - difference);
            Assert.That(difference, Is.LessThan(2));
            Assert.That(analyzer.Result.AngleDegrees, Is.GreaterThanOrEqualTo(-90).And.LessThan(90));
            Assert.That(analyzer.Result.Confidence, Is.GreaterThan(0.85));
        }

        [Test]
        public void CircularComponentIsVisibleButItsAxisIsRejected()
        {
            const int size = 96;
            var analyzer = Calibrated(Filled(size, size, 2000), size, size);
            var depth = Filled(size, size, 2000);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if ((x - 48) * (x - 48) + (y - 48) * (y - 48) <= 18 * 18)
                        depth[y * size + x] = 1700;
            analyzer.Process(depth, size, size, 100, 500);

            Assert.That(analyzer.Result.Valid, Is.False);
            Assert.That(analyzer.Result.Confidence, Is.LessThan(0.1));
            Assert.That(analyzer.Result.PixelCount, Is.GreaterThan(900));
            Assert.That(analyzer.Mask[48 * size + 48], Is.EqualTo(255));
        }

        [Test]
        public void LargestConnectedComponentWinsOverSeparateNoise()
        {
            const int size = 96;
            var analyzer = Calibrated(Filled(size, size, 2000), size, size);
            var depth = Filled(size, size, 2000);
            Rectangle(depth, size, size, 48, 48, 27, 5, 30, 1700);
            Rectangle(depth, size, size, 15, 12, 7, 4, 0, 1700);
            depth[90 * size + 90] = 1700;
            depth[5 * size + 80] = 1700;
            analyzer.Process(depth, size, size, 100, 500);

            Assert.That(analyzer.Result.Valid, Is.True);
            Assert.That(analyzer.Result.CenterX, Is.EqualTo(48).Within(0.5));
            Assert.That(analyzer.Result.CenterY, Is.EqualTo(48).Within(0.5));
            Assert.That(analyzer.Mask[12 * size + 15], Is.Zero);
            Assert.That(analyzer.Mask[90 * size + 90], Is.Zero);
            Assert.That(analyzer.Mask[48 * size + 48], Is.EqualTo(255));
        }

        [Test]
        public void HeightBandExcludesObjectsTooNearOrTooFar()
        {
            const int size = 96;
            var analyzer = Calibrated(Filled(size, size, 2000), size, size);
            var depth = Filled(size, size, 2000);
            Rectangle(depth, size, size, 48, 48, 18, 3, 0, 1700);
            Rectangle(depth, size, size, 48, 15, 30, 7, 0, 1950);
            Rectangle(depth, size, size, 48, 80, 30, 7, 0, 1300);
            analyzer.Process(depth, size, size, 100, 500);

            Assert.That(analyzer.Result.Valid, Is.True);
            Assert.That(analyzer.Result.CenterY, Is.EqualTo(48).Within(0.5));
            Assert.That(analyzer.Mask[15 * size + 48], Is.Zero);
            Assert.That(analyzer.Mask[80 * size + 48], Is.Zero);
        }

        [Test]
        public void TinyElongatedSpeckIsRejected()
        {
            const int size = 64;
            var analyzer = Calibrated(Filled(size, size, 2000), size, size);
            var depth = Filled(size, size, 2000);
            Rectangle(depth, size, size, 30, 30, 4, 0, 0, 1700);
            analyzer.Process(depth, size, size, 100, 500);

            Assert.That(analyzer.Result.PixelCount, Is.EqualTo(9));
            Assert.That(analyzer.Result.Valid, Is.False);
        }

        [Test]
        public void DisappearingObjectClearsPreviousAxisAndMask()
        {
            const int size = 64;
            var empty = Filled(size, size, 2000);
            var analyzer = Calibrated(empty, size, size);
            var depth = Filled(size, size, 2000);
            Rectangle(depth, size, size, 32, 32, 18, 3, 0, 1700);
            analyzer.Process(depth, size, size, 100, 500);
            Assert.That(analyzer.Result.Valid, Is.True);
            analyzer.Process(empty, size, size, 100, 500);

            Assert.That(analyzer.Result.Valid, Is.False);
            Assert.That(analyzer.Result.PixelCount, Is.Zero);
            Assert.That(Array.FindIndex(analyzer.Mask, p => p != 0), Is.EqualTo(-1));
        }

        [Test]
        public void ResolutionChangeRecalibratesWithTheRequestedFrameCount()
        {
            var analyzer = new DepthAnalyzer();
            analyzer.BeginCalibration(2);
            analyzer.Process(Filled(64, 64, 2000), 64, 64, 100, 500);
            analyzer.Process(Filled(64, 64, 2000), 64, 64, 100, 500);
            Assert.That(analyzer.Calibrated, Is.True);

            analyzer.Process(Filled(32, 24, 1500), 32, 24, 100, 500);
            Assert.That(analyzer.Width, Is.EqualTo(32));
            Assert.That(analyzer.Height, Is.EqualTo(24));
            Assert.That(analyzer.Calibrated, Is.False);
            Assert.That(analyzer.CalibrationFramesRemaining, Is.EqualTo(1));
            Assert.That(analyzer.Mask.Length, Is.EqualTo(32 * 24));
            analyzer.Process(Filled(32, 24, 1500), 32, 24, 100, 500);
            Assert.That(analyzer.Calibrated, Is.True);
            Assert.That(analyzer.Background[0], Is.EqualTo(1500));
        }

        private static DepthAnalyzer Calibrated(ushort[] background, int width, int height)
        {
            var analyzer = new DepthAnalyzer();
            analyzer.BeginCalibration(1);
            analyzer.Process(background, width, height, 100, 500);
            return analyzer;
        }

        private static ushort[] Filled(int width, int height, ushort value)
        {
            var result = new ushort[width * height];
            for (int i = 0; i < result.Length; i++)
                result[i] = value;
            return result;
        }

        private static void Rectangle(ushort[] depth, int width, int height, float cx, float cy,
            float halfLength, float halfThickness, float angleDegrees, ushort value)
        {
            double angle = angleDegrees * Math.PI / 180;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double along = (x - cx) * cos + (y - cy) * sin;
                    double across = -(x - cx) * sin + (y - cy) * cos;
                    if (Math.Abs(along) <= halfLength + 1e-6 && Math.Abs(across) <= halfThickness + 1e-6)
                        depth[y * width + x] = value;
                }
            }
        }
    }
}
