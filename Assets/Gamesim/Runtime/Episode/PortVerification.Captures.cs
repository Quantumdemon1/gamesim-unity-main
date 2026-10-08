using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Episode
{
    public sealed partial class PortVerification
    {
        /// <summary>Native screen evidence only: keep failed images and fail closed on absent presentation.</summary>
        private IEnumerator CaptureVerifiedFrame(string path, Action<VerificationFrameEvidence> record,
            Action<string> reject, Action waiting = null)
        {
            var frame = new VerificationFrameEvidence { path = path };
            record(frame);
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                frame.failure = "Native graphical captures require a windowed player without -batchmode/-nographics.";
                reject(frame.failure + " " + path); yield break;
            }
            if (File.Exists(path))
            {
                frame.failure = "Capture requires a fresh output path; the existing image was retained.";
                reject(frame.failure + " " + path); yield break;
            }
            int width = Screen.width, height = Screen.height;
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            byte[] complete = null;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(path);
                        if (VerificationCaptureValidation.IsCompletePng(bytes)) { complete = bytes; break; }
                    }
                    catch (IOException) { /* The asynchronous writer may still own the file. */ }
                }
                yield return null;
                waiting?.Invoke();
            }
            if (!VerificationCaptureValidation.Inspect(complete, width, height, frame))
                reject(frame.failure + " " + path);
        }
    }
}
