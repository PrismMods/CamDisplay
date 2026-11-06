using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CamDisplay
{
    /* What a camera can deliver on macOS, asked of AVFoundation directly: Unity's
       WebCamDevice.availableResolutions is null on desktop. Plain objc_msgSend calls, no native
       plugin to ship or sign. Each format gives its size (CMVideoFormatDescriptionGetDimensions)
       and its frame rate ranges, as {width, height, minFps, maxFps} for CamModes.From.

       Unity names a camera by its localizedName, so that is what this matches. Listing devices and
       formats needs no camera permission. Any failure (an API gone in a future macOS, a camera
       unplugged mid-call) gives an empty list, and the settings fall back to the fixed modes. */
    internal static class MacCamModes
    {
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        private const string Sys = "/usr/lib/libSystem.B.dylib";
        private const string AVFoundation = "/System/Library/Frameworks/AVFoundation.framework/AVFoundation";
        private const string CoreMedia = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";

        private static bool _logged;

        internal static List<double[]> Ranges(string device)
        {
            var list = new List<double[]>();
            if (!MacPatch.IsMac || string.IsNullOrEmpty(device)) return list;
            try
            {
                IntPtr constant = dlsym(dlopen(AVFoundation, 1), "AVMediaTypeVideo");
                if (constant == IntPtr.Zero) return list;
                IntPtr devices = Send(objc_getClass("AVCaptureDevice"), Sel("devicesWithMediaType:"), Marshal.ReadIntPtr(constant));
                for (ulong i = 0, n = Count(devices); i < n; i++)
                {
                    IntPtr d = At(devices, i);
                    if (Text(Send(d, Sel("localizedName"))) != device) continue;
                    IntPtr formats = Send(d, Sel("formats"));
                    for (ulong j = 0, nf = Count(formats); j < nf; j++)
                    {
                        IntPtr f = At(formats, j);
                        var size = CMVideoFormatDescriptionGetDimensions(Send(f, Sel("formatDescription")));
                        IntPtr rates = Send(f, Sel("videoSupportedFrameRateRanges"));
                        for (ulong k = 0, nr = Count(rates); k < nr; k++)
                        {
                            IntPtr r = At(rates, k);
                            list.Add(new double[] { size.Width, size.Height, Double(r, Sel("minFrameRate")), Double(r, Sel("maxFrameRate")) });
                        }
                    }
                    break;
                }
            }
            catch (Exception e)
            {
                if (!_logged) { _logged = true; Log.Error("listing the camera's modes", e); }
                list.Clear();
            }
            return list;
        }

        private static IntPtr Sel(string name) => sel_registerName(name);
        private static ulong Count(IntPtr array) => array == IntPtr.Zero ? 0 : (ulong)SendCount(array, Sel("count"));
        private static IntPtr At(IntPtr array, ulong i) => SendAt(array, Sel("objectAtIndex:"), (UIntPtr)i);
        private static double Double(IntPtr o, IntPtr sel) => SendDouble(o, sel);

        // An NSString's UTF-8 bytes: camera names are not always ASCII.
        private static string Text(IntPtr ns)
        {
            IntPtr p = ns == IntPtr.Zero ? IntPtr.Zero : Send(ns, Sel("UTF8String"));
            if (p == IntPtr.Zero) return "";
            int n = 0;
            while (Marshal.ReadByte(p, n) != 0) n++;
            var b = new byte[n];
            Marshal.Copy(p, b, 0, n);
            return Encoding.UTF8.GetString(b);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Dimensions { public int Width, Height; }

        [DllImport(Sys)] private static extern IntPtr dlopen(string path, int mode);
        [DllImport(Sys)] private static extern IntPtr dlsym(IntPtr handle, string name);
        [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr sel);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr sel, IntPtr arg);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendAt(IntPtr receiver, IntPtr sel, UIntPtr index);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern UIntPtr SendCount(IntPtr receiver, IntPtr sel);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern double SendDouble(IntPtr receiver, IntPtr sel);
        [DllImport(CoreMedia)] private static extern Dimensions CMVideoFormatDescriptionGetDimensions(IntPtr description);
    }
}
