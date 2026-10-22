using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class GameLayoutTests
    {
        [TestCase(1440f, 900f, 0f, 0f, 720f, 450f, 1f)]
        [TestCase(1920f, 1080f, 96f, 0f, 960f, 540f, 1.2f)]
        [TestCase(1024f, 768f, 0f, 64f, 512f, 384f, 0.7111111f)]
        public void CanvasFitsWithExpectedMarginsAndUniformDrawingScale(float width, float height,
            float marginX, float marginY, float centerX, float centerY, float expectedScale)
        {
            AssertPoint(GameLayout.Offset(width, height), marginX, marginY);
            AssertPoint(GameLayout.CanvasToScreen(Vector2.zero, width, height), marginX, marginY);
            AssertPoint(GameLayout.CanvasToScreen(new Vector2(1440, 900), width, height),
                width - marginX, height - marginY);
            AssertPoint(GameLayout.CanvasToScreen(new Vector2(720, 450), width, height), centerX, centerY);

            Matrix4x4 drawing = GameLayout.Matrix(width, height);
            AssertPoint(drawing.MultiplyPoint3x4(new Vector3(720, 450, 0)), centerX, centerY);
            Assert.That(drawing.MultiplyVector(Vector3.right).magnitude,
                Is.EqualTo(expectedScale).Within(0.001f), "Horizontal drawing scale");
            Assert.That(drawing.MultiplyVector(Vector3.up).magnitude,
                Is.EqualTo(expectedScale).Within(0.001f), "Vertical drawing scale must preserve circles");
        }

        [TestCase(1440f, 900f, 720f, 530f)]
        [TestCase(1920f, 1080f, 960f, 636f)]
        [TestCase(1024f, 768f, 512f, 440.8889f)]
        public void PointerAtDisplayedGateCenterMapsToTheOriginalGamePivot(float width, float height,
            float pointerX, float pointerY)
        {
            // Pointer coordinates already use the same top-left origin as the GUI.
            Vector2 canvas = GameLayout.ScreenToCanvas(new Vector2(pointerX, pointerY), width, height);
            AssertPoint(canvas, 720, 530);
            Assert.That(GameLayout.PlayArea.Contains(canvas), Is.True);

            Vector2 game = canvas - new Vector2(GameLayout.StageOffsetX, 0);
            AssertPoint(game, 505, 530);
        }

        [TestCase(1920f, 1080f, 48f, 540f, -40f, 450f)]
        [TestCase(1024f, 768f, 512f, 32f, 720f, -45f)]
        public void LetterboxPointerRemainsOutsideThePlayableArea(float width, float height,
            float pointerX, float pointerY, float expectedX, float expectedY)
        {
            Vector2 canvas = GameLayout.ScreenToCanvas(new Vector2(pointerX, pointerY), width, height);
            AssertPoint(canvas, expectedX, expectedY);
            Assert.That(GameLayout.PlayArea.Contains(canvas), Is.False,
                "A click in the surrounding margin must not steer the gate");
        }

        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(45f)]
        public void RotationKeepsThePivotFixedInsideTheTranslatedStage(float degrees)
        {
            Matrix4x4 drawing = GameLayout.Matrix(1920, 1080)
                * Matrix4x4.Translate(new Vector3(GameLayout.StageOffsetX, 0, 0))
                * GameLayout.RotationAround(new Vector2(505, 530), degrees);

            // Stage center is canvas (720, 530), or screen (960, 636) at this resolution.
            AssertPoint(drawing.MultiplyPoint3x4(new Vector3(505, 530, 0)), 960, 636);
        }

        [Test]
        public void QuarterTurnMovesARightEdgeBelowTheSameScreenCenter()
        {
            Matrix4x4 drawing = GameLayout.Matrix(1920, 1080)
                * Matrix4x4.Translate(new Vector3(GameLayout.StageOffsetX, 0, 0))
                * GameLayout.RotationAround(new Vector2(505, 530), 90);

            // A point 20 local pixels to the right becomes 24 screen pixels below the pivot.
            AssertPoint(drawing.MultiplyPoint3x4(new Vector3(525, 530, 0)), 960, 660);
        }

        private static void AssertPoint(Vector2 actual, float x, float y)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(0.001f), "X coordinate");
            Assert.That(actual.y, Is.EqualTo(y).Within(0.001f), "Y coordinate");
        }
    }
}
