// Windows only (editor and standalone player): reads a webcam through Media Foundation in its compressed MJPG
// mode and hands out decoded RGBA frames. Unity's WebCamTexture cannot choose the camera's format and on many
// USB webcams picks uncompressed YUY2, which the USB bandwidth caps at ~10 fps at 720p and ~5 fps at 1080p
// (measured on a Logitech C920, 2026-11-06). The same cameras deliver 30 fps in MJPG. Capture and decoding run on
// a background thread; WebCamSource uploads the newest frame to a texture on the main thread.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || MF_CAPTURE_TEST
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mediapipe.Unity
{
    public sealed class MfCameraCapture : IDisposable
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public double NativeFps { get; private set; }
        public string FormatName { get; private set; } = "";
        public string DeviceName { get; private set; } = "";
        /// <summary>Increments for every decoded frame; compare to know when CopyLatest has something new.</summary>
        public long FrameCount => Interlocked.Read(ref _frameCount);
        public string Error { get; private set; }

        readonly object _lock = new object();
        byte[] _latest;          // RGBA32, bottom row first (Unity texture layout, like WebCamTexture)
        long _frameCount;
        Thread _thread;
        volatile bool _stop;
        readonly ManualResetEvent _opened = new ManualResetEvent(false);
        bool _openOk;

        MfCameraCapture() { }

        /// <summary>
        /// Opens the camera whose friendly name contains <paramref name="nameHint"/> (or the first one) in MJPG at the
        /// size closest to width x height with the highest frame rate. Returns null when there is no such mode.
        /// </summary>
        public static MfCameraCapture Open(string nameHint, int width, int height, int minFps, int timeoutMs, out string error)
        {
            var capture = new MfCameraCapture();
            capture._thread = new Thread(() => capture.Run(nameHint, width, height, minFps)) { IsBackground = true, Name = "MJPG camera" };
            capture._thread.Start();
            if (!capture._opened.WaitOne(timeoutMs) || !capture._openOk)
            {
                error = capture.Error ?? "timed out opening the camera";
                capture.Dispose();
                return null;
            }
            error = null;
            return capture;
        }

        /// <summary>Copies the newest frame into dest (Width*Height*4 bytes). False when no frame arrived yet.</summary>
        public bool CopyLatest(byte[] dest)
        {
            lock (_lock)
            {
                if (_latest == null || dest.Length < _latest.Length) return false;
                Buffer.BlockCopy(_latest, 0, dest, 0, _latest.Length);
                return true;
            }
        }

        public void Dispose()
        {
            _stop = true;
            if (_thread != null && _thread.IsAlive && Thread.CurrentThread != _thread) _thread.Join(2000);
            _thread = null;
        }

        void Run(string nameHint, int width, int height, int minFps)
        {
            IMFMediaSource source = null;
            IMFSourceReader reader = null;
            try
            {
                Check(MFStartup(MF_VERSION, 0), "MFStartup");
                source = CreateSource(nameHint);
                if (source == null) { Error = "no Media Foundation video capture device"; return; }

                // Pick the device's MJPG mode: closest size, then the highest frame rate.
                Check(source.CreatePresentationDescriptor(out IMFPresentationDescriptor pd), "presentation descriptor");
                Check(pd.GetStreamDescriptorByIndex(0, out bool _, out IMFStreamDescriptor sd), "stream descriptor");
                Check(sd.GetMediaTypeHandler(out IMFMediaTypeHandler handler), "media type handler");
                Check(handler.GetMediaTypeCount(out int count), "media type count");
                IMFMediaType best = null; long bestScore = long.MaxValue; int bw = 0, bh = 0; double bfps = 0;
                for (int i = 0; i < count; i++)
                {
                    if (handler.GetMediaTypeByIndex(i, out IMFMediaType type) != 0) continue;
                    var sub = MF_MT_SUBTYPE;
                    if (type.GetGUID(ref sub, out Guid subtype) != 0 || subtype != MFVideoFormat_MJPG) { Marshal.ReleaseComObject(type); continue; }
                    var fs = MF_MT_FRAME_SIZE; var fr = MF_MT_FRAME_RATE;
                    type.GetUINT64(ref fs, out long size); type.GetUINT64(ref fr, out long rate);
                    int w = (int)(size >> 32), h = (int)(size & 0xffffffff);
                    double fps = (rate & 0xffffffff) == 0 ? 0 : (double)(rate >> 32) / (rate & 0xffffffff);
                    long score = (long)Math.Abs(w * h - width * height) * 100 + (fps >= minFps ? 0 : 1L << 40) - (long)fps;
                    if (score < bestScore) { if (best != null) Marshal.ReleaseComObject(best); best = type; bestScore = score; bw = w; bh = h; bfps = fps; }
                    else Marshal.ReleaseComObject(type);
                }
                if (best == null) { Error = "the camera has no MJPG mode"; return; }
                Check(handler.SetCurrentMediaType(best), "set MJPG mode");

                // The reader decodes MJPG and converts to RGB32 (MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING).
                Check(MFCreateAttributes(out IMFAttributes readerAttributes, 2), "reader attributes");
                var vp = MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING;
                readerAttributes.SetUINT32(ref vp, 1);
                Check(MFCreateSourceReaderFromMediaSource(source, readerAttributes, out reader), "source reader");
                Check(MFCreateMediaType(out IMFMediaType output), "output type");
                var major = MF_MT_MAJOR_TYPE; var video = MFMediaType_Video; var subKey = MF_MT_SUBTYPE; var rgb = MFVideoFormat_RGB32;
                output.SetGUID(ref major, ref video);
                output.SetGUID(ref subKey, ref rgb);
                var frameSize = MF_MT_FRAME_SIZE;
                output.SetUINT64(ref frameSize, ((long)bw << 32) | (uint)bh);
                Check(reader.SetCurrentMediaType(FirstVideoStream, IntPtr.Zero, output), "set RGB32 output");
                Check(reader.GetCurrentMediaType(FirstVideoStream, out IMFMediaType current), "current type");
                var strideKey = MF_MT_DEFAULT_STRIDE;
                int stride = current.GetUINT32(ref strideKey, out int s) == 0 ? s : -bw * 4;   // RGB defaults to bottom-up
                Width = bw; Height = bh; NativeFps = bfps; FormatName = "MJPG";
                _openOk = true;
                _opened.Set();

                var rgba = new byte[bw * bh * 4];
                while (!_stop)
                {
                    int hr = reader.ReadSample(FirstVideoStream, 0, out int _, out int flags, out long _, out IMFSample sample);
                    if (hr != 0) { Error = $"ReadSample 0x{hr:X8}"; break; }
                    if ((flags & 2) != 0) { Error = "camera stream ended"; break; }   // MF_SOURCE_READERF_ENDOFSTREAM
                    if (sample == null) continue;
                    try
                    {
                        if (sample.ConvertToContiguousBuffer(out IMFMediaBuffer buffer) != 0) continue;
                        try
                        {
                            if (buffer.Lock(out IntPtr data, out int _, out int length) != 0) continue;
                            try { ConvertToRgba(data, length, stride, bw, bh, rgba); }
                            finally { buffer.Unlock(); }
                        }
                        finally { Marshal.ReleaseComObject(buffer); }
                    }
                    finally { Marshal.ReleaseComObject(sample); }
                    lock (_lock)
                    {
                        if (_latest == null) _latest = new byte[rgba.Length];
                        Buffer.BlockCopy(rgba, 0, _latest, 0, rgba.Length);
                    }
                    Interlocked.Increment(ref _frameCount);
                }
            }
            catch (Exception e) { Error = e.Message; }
            finally
            {
                _opened.Set();
                if (reader != null) Marshal.ReleaseComObject(reader);
                if (source != null) { source.Shutdown(); Marshal.ReleaseComObject(source); }
                MFShutdown();
            }
        }

        // BGRX rows (stride sign = row order) -> RGBA with the bottom row first.
        // (No unsafe code: the UnitEye assemblies do not allow it.)
        static void ConvertToRgba(IntPtr data, int length, int stride, int w, int h, byte[] rgba)
        {
            int rowBytes = w * 4, abs = Math.Abs(stride);
            if (length < abs * (h - 1) + rowBytes) return;
            for (int y = 0; y < h; y++)
            {
                // Memory row y: top-down when stride > 0, bottom-up when < 0. Output row 0 is the bottom.
                int outRow = stride > 0 ? h - 1 - y : y;
                Marshal.Copy(IntPtr.Add(data, y * abs), rgba, outRow * rowBytes, rowBytes);
            }
            for (int i = 0; i < rgba.Length; i += 4) { byte b = rgba[i]; rgba[i] = rgba[i + 2]; rgba[i + 2] = b; rgba[i + 3] = 255; }
        }

        IMFMediaSource CreateSource(string nameHint)
        {
            Check(MFCreateAttributes(out IMFAttributes attributes, 1), "device attributes");
            var key = MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE; var vidcap = MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;
            attributes.SetGUID(ref key, ref vidcap);
            Check(MFEnumDeviceSources(attributes, out IntPtr devices, out int count), "enumerate cameras");
            IMFMediaSource chosen = null;
            try
            {
                int pick = 0;
                var names = new string[count];
                for (int i = 0; i < count; i++)
                {
                    var activate = (IMFActivate)Marshal.GetObjectForIUnknown(Marshal.ReadIntPtr(devices, i * IntPtr.Size));
                    var nameKey = MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME;
                    if (activate.GetAllocatedString(ref nameKey, out IntPtr text, out int _) == 0)
                    {
                        names[i] = Marshal.PtrToStringUni(text);
                        Marshal.FreeCoTaskMem(text);
                    }
                    if (!string.IsNullOrEmpty(nameHint) && names[i] != null &&
                        (names[i].IndexOf(nameHint, StringComparison.OrdinalIgnoreCase) >= 0 || nameHint.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) >= 0))
                        pick = i;
                }
                if (count > 0)
                {
                    var activate = (IMFActivate)Marshal.GetObjectForIUnknown(Marshal.ReadIntPtr(devices, pick * IntPtr.Size));
                    var iid = typeof(IMFMediaSource).GUID;
                    Check(activate.ActivateObject(ref iid, out IntPtr ppv), "activate camera");
                    chosen = (IMFMediaSource)Marshal.GetObjectForIUnknown(ppv);
                    Marshal.Release(ppv);
                    DeviceName = names[pick] ?? "";
                }
                for (int i = 0; i < count; i++) Marshal.Release(Marshal.ReadIntPtr(devices, i * IntPtr.Size));
            }
            finally { Marshal.FreeCoTaskMem(devices); }
            return chosen;
        }

        static void Check(int hr, string what)
        {
            if (hr < 0) throw new Exception($"Media Foundation: {what} failed (0x{hr:X8})");
        }

        // ---- Media Foundation interop ----------------------------------------------------------------------
        const int MF_VERSION = 0x00020070;
        const int FirstVideoStream = unchecked((int)0xFFFFFFFC);
        static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE = new Guid("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");
        static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID = new Guid("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
        static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME = new Guid("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");
        static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        static readonly Guid MF_MT_FRAME_RATE = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        static readonly Guid MF_MT_DEFAULT_STRIDE = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
        static readonly Guid MFVideoFormat_MJPG = new Guid("47504A4D-0000-0010-8000-00AA00389B71");
        static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00AA00389B71");
        static readonly Guid MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING = new Guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");

        [DllImport("mfplat.dll")] static extern int MFStartup(int version, int flags);
        [DllImport("mfplat.dll")] static extern int MFShutdown();
        [DllImport("mfplat.dll")] static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);
        [DllImport("mfplat.dll")] static extern int MFCreateMediaType(out IMFMediaType type);
        [DllImport("mf.dll")] static extern int MFEnumDeviceSources(IMFAttributes attributes, out IntPtr activates, out int count);
        [DllImport("mfreadwrite.dll")] static extern int MFCreateSourceReaderFromMediaSource(IMFMediaSource source, IMFAttributes attributes, out IMFSourceReader reader);

        // COM interfaces list every vtable slot in order (C# COM interop does not inherit base interfaces).
        [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFAttributes
        {
            [PreserveSig] int GetItem(ref Guid key, IntPtr value);
            [PreserveSig] int GetItemType(ref Guid key, out int type);
            [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out bool result);
            [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool result);
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            [PreserveSig] int GetDouble(ref Guid key, out double value);
            [PreserveSig] int GetGUID(ref Guid key, out Guid value);
            [PreserveSig] int GetStringLength(ref Guid key, out int length);
            [PreserveSig] int GetString(ref Guid key, IntPtr buffer, int size, IntPtr length);
            [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr text, out int length);
            [PreserveSig] int GetBlobSize(ref Guid key, out int size);
            [PreserveSig] int GetBlob(ref Guid key, IntPtr buffer, int size, IntPtr written);
            [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr buffer, out int size);
            [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
            [PreserveSig] int SetItem(ref Guid key, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid key);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            [PreserveSig] int SetUINT64(ref Guid key, long value);
            [PreserveSig] int SetDouble(ref Guid key, double value);
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            [PreserveSig] int SetString(ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
            [PreserveSig] int SetBlob(ref Guid key, IntPtr buffer, int size);
            [PreserveSig] int SetUnknown(ref Guid key, IntPtr value);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
            [PreserveSig] int CopyAllItems(IMFAttributes dest);
        }

        [ComImport, Guid("7FEE9E9A-4A89-47a6-899C-B6A53A70FB67"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFActivate
        {
            [PreserveSig] int GetItem(ref Guid key, IntPtr value);
            [PreserveSig] int GetItemType(ref Guid key, out int type);
            [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out bool result);
            [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool result);
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            [PreserveSig] int GetDouble(ref Guid key, out double value);
            [PreserveSig] int GetGUID(ref Guid key, out Guid value);
            [PreserveSig] int GetStringLength(ref Guid key, out int length);
            [PreserveSig] int GetString(ref Guid key, IntPtr buffer, int size, IntPtr length);
            [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr text, out int length);
            [PreserveSig] int GetBlobSize(ref Guid key, out int size);
            [PreserveSig] int GetBlob(ref Guid key, IntPtr buffer, int size, IntPtr written);
            [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr buffer, out int size);
            [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
            [PreserveSig] int SetItem(ref Guid key, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid key);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            [PreserveSig] int SetUINT64(ref Guid key, long value);
            [PreserveSig] int SetDouble(ref Guid key, double value);
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            [PreserveSig] int SetString(ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
            [PreserveSig] int SetBlob(ref Guid key, IntPtr buffer, int size);
            [PreserveSig] int SetUnknown(ref Guid key, IntPtr value);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
            [PreserveSig] int CopyAllItems(IMFAttributes dest);
            [PreserveSig] int ActivateObject(ref Guid riid, out IntPtr ppv);
            [PreserveSig] int ShutdownObject();
            [PreserveSig] int DetachObject();
        }

        [ComImport, Guid("279a808d-aec7-40c8-9c6b-a6b492c78a66"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFMediaSource
        {
            [PreserveSig] int GetEvent(int flags, out IntPtr ev);
            [PreserveSig] int BeginGetEvent(IntPtr callback, IntPtr state);
            [PreserveSig] int EndGetEvent(IntPtr result, out IntPtr ev);
            [PreserveSig] int QueueEvent(int type, ref Guid extended, int status, IntPtr value);
            [PreserveSig] int GetCharacteristics(out int characteristics);
            [PreserveSig] int CreatePresentationDescriptor(out IMFPresentationDescriptor descriptor);
            [PreserveSig] int Start(IntPtr descriptor, IntPtr timeFormat, IntPtr start);
            [PreserveSig] int Stop();
            [PreserveSig] int Pause();
            [PreserveSig] int Shutdown();
        }

        [ComImport, Guid("03cb2711-24d7-4db6-a17f-f3a7a479a536"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFPresentationDescriptor
        {
            [PreserveSig] int GetItem(ref Guid key, IntPtr value);
            [PreserveSig] int GetItemType(ref Guid key, out int type);
            [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out bool result);
            [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool result);
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            [PreserveSig] int GetDouble(ref Guid key, out double value);
            [PreserveSig] int GetGUID(ref Guid key, out Guid value);
            [PreserveSig] int GetStringLength(ref Guid key, out int length);
            [PreserveSig] int GetString(ref Guid key, IntPtr buffer, int size, IntPtr length);
            [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr text, out int length);
            [PreserveSig] int GetBlobSize(ref Guid key, out int size);
            [PreserveSig] int GetBlob(ref Guid key, IntPtr buffer, int size, IntPtr written);
            [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr buffer, out int size);
            [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
            [PreserveSig] int SetItem(ref Guid key, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid key);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            [PreserveSig] int SetUINT64(ref Guid key, long value);
            [PreserveSig] int SetDouble(ref Guid key, double value);
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            [PreserveSig] int SetString(ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
            [PreserveSig] int SetBlob(ref Guid key, IntPtr buffer, int size);
            [PreserveSig] int SetUnknown(ref Guid key, IntPtr value);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
            [PreserveSig] int CopyAllItems(IMFAttributes dest);
            [PreserveSig] int GetStreamDescriptorCount(out int count);
            [PreserveSig] int GetStreamDescriptorByIndex(int index, out bool selected, out IMFStreamDescriptor descriptor);
            [PreserveSig] int SelectStream(int index);
            [PreserveSig] int DeselectStream(int index);
            [PreserveSig] int Clone(out IMFPresentationDescriptor copy);
        }

        [ComImport, Guid("56c03d9c-9dbb-45f5-ab4b-d80f47c05938"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFStreamDescriptor
        {
            [PreserveSig] int GetItem(ref Guid key, IntPtr value);
            [PreserveSig] int GetItemType(ref Guid key, out int type);
            [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out bool result);
            [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool result);
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            [PreserveSig] int GetDouble(ref Guid key, out double value);
            [PreserveSig] int GetGUID(ref Guid key, out Guid value);
            [PreserveSig] int GetStringLength(ref Guid key, out int length);
            [PreserveSig] int GetString(ref Guid key, IntPtr buffer, int size, IntPtr length);
            [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr text, out int length);
            [PreserveSig] int GetBlobSize(ref Guid key, out int size);
            [PreserveSig] int GetBlob(ref Guid key, IntPtr buffer, int size, IntPtr written);
            [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr buffer, out int size);
            [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
            [PreserveSig] int SetItem(ref Guid key, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid key);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            [PreserveSig] int SetUINT64(ref Guid key, long value);
            [PreserveSig] int SetDouble(ref Guid key, double value);
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            [PreserveSig] int SetString(ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
            [PreserveSig] int SetBlob(ref Guid key, IntPtr buffer, int size);
            [PreserveSig] int SetUnknown(ref Guid key, IntPtr value);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
            [PreserveSig] int CopyAllItems(IMFAttributes dest);
            [PreserveSig] int GetStreamIdentifier(out int id);
            [PreserveSig] int GetMediaTypeHandler(out IMFMediaTypeHandler handler);
        }

        [ComImport, Guid("e93dcf6c-4b07-4e1e-8123-aa16ed6eadf5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFMediaTypeHandler
        {
            [PreserveSig] int IsMediaTypeSupported(IMFMediaType type, IntPtr closest);
            [PreserveSig] int GetMediaTypeCount(out int count);
            [PreserveSig] int GetMediaTypeByIndex(int index, out IMFMediaType type);
            [PreserveSig] int SetCurrentMediaType(IMFMediaType type);
            [PreserveSig] int GetCurrentMediaType(out IMFMediaType type);
            [PreserveSig] int GetMajorType(out Guid major);
        }

        [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFMediaType
        {
            [PreserveSig] int GetItem(ref Guid key, IntPtr value);
            [PreserveSig] int GetItemType(ref Guid key, out int type);
            [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out bool result);
            [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool result);
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            [PreserveSig] int GetDouble(ref Guid key, out double value);
            [PreserveSig] int GetGUID(ref Guid key, out Guid value);
            [PreserveSig] int GetStringLength(ref Guid key, out int length);
            [PreserveSig] int GetString(ref Guid key, IntPtr buffer, int size, IntPtr length);
            [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr text, out int length);
            [PreserveSig] int GetBlobSize(ref Guid key, out int size);
            [PreserveSig] int GetBlob(ref Guid key, IntPtr buffer, int size, IntPtr written);
            [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr buffer, out int size);
            [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
            [PreserveSig] int SetItem(ref Guid key, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid key);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            [PreserveSig] int SetUINT64(ref Guid key, long value);
            [PreserveSig] int SetDouble(ref Guid key, double value);
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            [PreserveSig] int SetString(ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
            [PreserveSig] int SetBlob(ref Guid key, IntPtr buffer, int size);
            [PreserveSig] int SetUnknown(ref Guid key, IntPtr value);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
            [PreserveSig] int CopyAllItems(IMFAttributes dest);
            [PreserveSig] int GetMajorType(out Guid major);
            [PreserveSig] int IsCompressedFormat(out bool compressed);
            [PreserveSig] int IsEqual(IMFMediaType other, out int flags);
            [PreserveSig] int GetRepresentation(Guid representation, out IntPtr data);
            [PreserveSig] int FreeRepresentation(Guid representation, IntPtr data);
        }

        [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFSourceReader
        {
            [PreserveSig] int GetStreamSelection(int stream, out bool selected);
            [PreserveSig] int SetStreamSelection(int stream, bool selected);
            [PreserveSig] int GetNativeMediaType(int stream, int index, out IMFMediaType type);
            [PreserveSig] int GetCurrentMediaType(int stream, out IMFMediaType type);
            [PreserveSig] int SetCurrentMediaType(int stream, IntPtr reserved, IMFMediaType type);
            [PreserveSig] int SetCurrentPosition(ref Guid format, IntPtr position);
            [PreserveSig] int ReadSample(int stream, int controlFlags, out int actualStream, out int streamFlags, out long timestamp, out IMFSample sample);
            [PreserveSig] int Flush(int stream);
            [PreserveSig] int GetServiceForStream(int stream, ref Guid service, ref Guid riid, out IntPtr obj);
            [PreserveSig] int GetPresentationAttribute(int stream, ref Guid attribute, IntPtr value);
        }

        [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFSample
        {
            [PreserveSig] int GetItem(ref Guid key, IntPtr value);
            [PreserveSig] int GetItemType(ref Guid key, out int type);
            [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out bool result);
            [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool result);
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            [PreserveSig] int GetDouble(ref Guid key, out double value);
            [PreserveSig] int GetGUID(ref Guid key, out Guid value);
            [PreserveSig] int GetStringLength(ref Guid key, out int length);
            [PreserveSig] int GetString(ref Guid key, IntPtr buffer, int size, IntPtr length);
            [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr text, out int length);
            [PreserveSig] int GetBlobSize(ref Guid key, out int size);
            [PreserveSig] int GetBlob(ref Guid key, IntPtr buffer, int size, IntPtr written);
            [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr buffer, out int size);
            [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
            [PreserveSig] int SetItem(ref Guid key, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid key);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            [PreserveSig] int SetUINT64(ref Guid key, long value);
            [PreserveSig] int SetDouble(ref Guid key, double value);
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            [PreserveSig] int SetString(ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
            [PreserveSig] int SetBlob(ref Guid key, IntPtr buffer, int size);
            [PreserveSig] int SetUnknown(ref Guid key, IntPtr value);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
            [PreserveSig] int CopyAllItems(IMFAttributes dest);
            [PreserveSig] int GetSampleFlags(out int flags);
            [PreserveSig] int SetSampleFlags(int flags);
            [PreserveSig] int GetSampleTime(out long time);
            [PreserveSig] int SetSampleTime(long time);
            [PreserveSig] int GetSampleDuration(out long duration);
            [PreserveSig] int SetSampleDuration(long duration);
            [PreserveSig] int GetBufferCount(out int count);
            [PreserveSig] int GetBufferByIndex(int index, out IMFMediaBuffer buffer);
            [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
            [PreserveSig] int AddBuffer(IMFMediaBuffer buffer);
            [PreserveSig] int RemoveBufferByIndex(int index);
            [PreserveSig] int RemoveAllBuffers();
            [PreserveSig] int GetTotalLength(out int length);
            [PreserveSig] int CopyToBuffer(IMFMediaBuffer buffer);
        }

        [ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFMediaBuffer
        {
            [PreserveSig] int Lock(out IntPtr data, out int maxLength, out int currentLength);
            [PreserveSig] int Unlock();
            [PreserveSig] int GetCurrentLength(out int length);
            [PreserveSig] int SetCurrentLength(int length);
            [PreserveSig] int GetMaxLength(out int length);
        }
    }
}
#endif
