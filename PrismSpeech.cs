using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Speech and braille output through Prism (github.com/ethindp/prism, MPL-2.0),
    /// which drives NVDA, JAWS, ZDSR, PC-Talker, BoyPC Reader, Sense Reader, ZoomText,
    /// System Access and Narrator on Windows, VoiceOver on macOS, Orca and
    /// speech-dispatcher on Linux, and falls back to the OS voice (SAPI/OneCore,
    /// AVSpeech) when no screen reader is running.
    ///
    /// The native library ships next to the plugin DLL (prism.dll, libprism.dylib,
    /// libprism.so) and is loaded by absolute path, with every export bound through
    /// a function pointer. That avoids depending on each platform's DllImport search
    /// rules, which differ between Mono builds and cannot be tested here on macOS
    /// or Linux.
    /// </summary>
    internal static class PrismSpeech
    {
        private const int PrismOk = 0;
        private const int PrismErrorNotImplemented = 3;
        private const int PrismErrorAlreadyInitialized = 15;
        private const ulong FeatureSupportedAtRuntime = 1UL << 0;

        // Plain TTS backends: used only when no screen reader is reachable, so while
        // one is active the mod keeps checking for a screen reader starting up.
        private const ulong BackendSapi = 0x1D6DF72422CEEE66;
        private const ulong BackendAvSpeech = 0x28E3429577805C24;
        private const ulong BackendSpeechDispatcher = 0xE3D6F895D949EBFE;
        private const ulong BackendOneCore = 0x6797D32F0D994CB4;

        private const int ScreenReaderProbeIntervalMs = 5000;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr InitFn(IntPtr config);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ContextFn(IntPtr ctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CreateBestFn(IntPtr ctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate UIntPtr RegistryCountFn(IntPtr ctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ulong RegistryIdAtFn(IntPtr ctx, UIntPtr index);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ulong RegistryIdFn(IntPtr ctx, byte[] utf8Name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RegistryPriorityFn(IntPtr ctx, ulong id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr RegistryCreateFn(IntPtr ctx, ulong id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr BackendNameFn(IntPtr backend);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ulong BackendFeaturesFn(IntPtr backend);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BackendCallFn(IntPtr backend);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BackendOutputFn(IntPtr backend, byte[] utf8Text,
            [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BackendFreeFn(IntPtr backend);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ErrorStringFn(int error);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr LinuxDlopen(string path, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        private static extern IntPtr LinuxDlsym(IntPtr handle, string name);

        [DllImport("libdl.so.2", EntryPoint = "dlerror")]
        private static extern IntPtr LinuxDlerror();

        [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlopen")]
        private static extern IntPtr MacDlopen(string path, int flags);

        [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlsym")]
        private static extern IntPtr MacDlsym(IntPtr handle, string name);

        [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlerror")]
        private static extern IntPtr MacDlerror();

        private const int RtldNow = 2;

        private enum NativePlatform { Windows, Mac, Linux }

        private static NativePlatform _platform;
        private static IntPtr _library;

        private static InitFn _init;
        private static ContextFn _shutdown;
        private static CreateBestFn _createBest;
        private static RegistryCountFn _registryCount;
        private static RegistryIdAtFn _registryIdAt;
        private static RegistryIdFn _registryId;
        private static RegistryPriorityFn _registryPriority;
        private static RegistryCreateFn _registryCreate;
        private static BackendNameFn _backendName;
        private static BackendFeaturesFn _backendFeatures;
        private static BackendCallFn _backendInitialize;
        private static BackendOutputFn _backendOutput;
        private static BackendCallFn _backendStop;
        private static BackendFreeFn _backendFree;
        private static ErrorStringFn _errorString;

        private static IntPtr _context;
        private static IntPtr _backend;
        private static ulong _backendId;
        private static int _backendPriority;
        private static int _lastProbeTick;
        private static bool _loggedOutputError;

        /// <summary>True once the library is loaded and a backend is speaking.</summary>
        public static bool IsAvailable
        {
            get { return _backend != IntPtr.Zero; }
        }

        /// <summary>Name of the active backend (e.g. "NVDA", "SAPI"), or empty.</summary>
        public static string BackendName { get; private set; }

        /// <summary>
        /// Loads Prism and selects the best available backend: a running screen reader
        /// first, otherwise the OS voice. Returns false (and logs why) when speech is
        /// unavailable; the mod keeps running silently in that case.
        /// </summary>
        public static bool Initialize()
        {
            BackendName = string.Empty;
            try
            {
                if (!LoadLibrary())
                    return false;

                _context = _init(IntPtr.Zero);
                if (_context == IntPtr.Zero)
                {
                    Main.Log.LogError("Prism: prism_init failed.");
                    return false;
                }

                return SelectBest();
            }
            catch (Exception ex)
            {
                Main.Log.LogError("Prism: initialization failed: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Speaks text and sends it to braille where the backend supports it. When the
        /// active backend fails (e.g. the screen reader was closed), re-selects the best
        /// backend and retries once.
        /// </summary>
        public static void Output(string text, bool interrupt)
        {
            if (_context == IntPtr.Zero)
                return;

            ProbeForScreenReader();
            if (_backend == IntPtr.Zero && !SelectBest())
                return;

            byte[] utf8 = ToUtf8(text);
            int result = _backendOutput(_backend, utf8, interrupt);
            if (result == PrismOk)
            {
                _loggedOutputError = false;
                return;
            }

            if (!_loggedOutputError)
            {
                _loggedOutputError = true;
                Main.Log.LogWarning("Prism: " + BackendName + " output failed (" + ErrorText(result)
                    + "); re-selecting backend.");
            }

            if (SelectBest())
                _backendOutput(_backend, utf8, interrupt);
        }

        /// <summary>
        /// Stops current speech. Some backends cannot stop (VoiceOver announcements on
        /// macOS); that is not an error.
        /// </summary>
        public static void Stop()
        {
            if (_backend == IntPtr.Zero)
                return;

            int result = _backendStop(_backend);
            if (result != PrismOk && result != PrismErrorNotImplemented)
                DebugLogger.Log(LogCategory.State, "Prism", "stop: " + ErrorText(result));
        }

        /// <summary>Releases the backend and the Prism context.</summary>
        public static void Shutdown()
        {
            try
            {
                FreeBackend();
                if (_context != IntPtr.Zero)
                {
                    _shutdown(_context);
                    _context = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("Prism: shutdown failed: " + ex.Message);
            }
        }

        private static bool LoadLibrary()
        {
            string fileName;
            switch (Application.platform)
            {
                case RuntimePlatform.OSXPlayer:
                    _platform = NativePlatform.Mac;
                    fileName = "libprism.dylib";
                    break;
                case RuntimePlatform.LinuxPlayer:
                    _platform = NativePlatform.Linux;
                    fileName = "libprism.so";
                    break;
                default:
                    _platform = NativePlatform.Windows;
                    fileName = "prism.dll";
                    break;
            }

            string path = Path.Combine(Path.GetDirectoryName(typeof(PrismSpeech).Assembly.Location), fileName);
            if (!File.Exists(path))
            {
                Main.Log.LogError("Prism: " + path + " is missing; speech is disabled.");
                return false;
            }

            switch (_platform)
            {
                case NativePlatform.Windows:
                    _library = LoadLibraryW(path);
                    if (_library == IntPtr.Zero)
                        Main.Log.LogError("Prism: could not load " + path + " (Windows error "
                            + Marshal.GetLastWin32Error() + ").");
                    break;
                case NativePlatform.Mac:
                    _library = MacDlopen(path, RtldNow);
                    if (_library == IntPtr.Zero)
                        Main.Log.LogError("Prism: could not load " + path + ": " + Utf8At(MacDlerror()));
                    break;
                default:
                    _library = LinuxDlopen(path, RtldNow);
                    if (_library == IntPtr.Zero)
                        Main.Log.LogError("Prism: could not load " + path + ": " + Utf8At(LinuxDlerror())
                            + ". On Linux, Prism needs glibmm 2.68, giomm 2.68 and libsigc++ 3 installed.");
                    break;
            }

            if (_library == IntPtr.Zero)
                return false;

            _init = Bind<InitFn>("prism_init");
            _shutdown = Bind<ContextFn>("prism_shutdown");
            _createBest = Bind<CreateBestFn>("prism_registry_create_best");
            _registryCount = Bind<RegistryCountFn>("prism_registry_count");
            _registryIdAt = Bind<RegistryIdAtFn>("prism_registry_id_at");
            _registryId = Bind<RegistryIdFn>("prism_registry_id");
            _registryPriority = Bind<RegistryPriorityFn>("prism_registry_priority");
            _registryCreate = Bind<RegistryCreateFn>("prism_registry_create");
            _backendName = Bind<BackendNameFn>("prism_backend_name");
            _backendFeatures = Bind<BackendFeaturesFn>("prism_backend_get_features");
            _backendInitialize = Bind<BackendCallFn>("prism_backend_initialize");
            _backendOutput = Bind<BackendOutputFn>("prism_backend_output");
            _backendStop = Bind<BackendCallFn>("prism_backend_stop");
            _backendFree = Bind<BackendFreeFn>("prism_backend_free");
            _errorString = Bind<ErrorStringFn>("prism_error_string");

            Main.Log.LogInfo("Prism: loaded " + path);
            return true;
        }

        private static T Bind<T>(string export) where T : class
        {
            IntPtr symbol;
            switch (_platform)
            {
                case NativePlatform.Windows:
                    symbol = GetProcAddress(_library, export);
                    break;
                case NativePlatform.Mac:
                    symbol = MacDlsym(_library, export);
                    break;
                default:
                    symbol = LinuxDlsym(_library, export);
                    break;
            }

            if (symbol == IntPtr.Zero)
                throw new EntryPointNotFoundException("Prism export not found: " + export);

            return Marshal.GetDelegateForFunctionPointer(symbol, typeof(T)) as T;
        }

        /// <summary>
        /// Replaces the active backend with the highest-priority one that initializes.
        /// </summary>
        private static bool SelectBest()
        {
            FreeBackend();

            IntPtr backend = _createBest(_context);
            if (backend == IntPtr.Zero)
            {
                Main.Log.LogWarning("Prism: no speech backend available (no screen reader and no OS voice).");
                return false;
            }

            Adopt(backend);
            Main.Log.LogInfo("Prism: speaking through " + BackendName + ".");
            return true;
        }

        /// <summary>
        /// While on a plain TTS backend, periodically tries the higher-priority backends
        /// (screen readers) so one started after the game takes over speech.
        /// </summary>
        private static void ProbeForScreenReader()
        {
            if (_backend == IntPtr.Zero || !IsTtsFallback(_backendId))
                return;

            int now = Environment.TickCount;
            if (unchecked(now - _lastProbeTick) < ScreenReaderProbeIntervalMs)
                return;
            _lastProbeTick = now;

            int count = (int)_registryCount(_context).ToUInt32();
            for (int i = 0; i < count; i++)
            {
                ulong id = _registryIdAt(_context, new UIntPtr((uint)i));
                if (_registryPriority(_context, id) <= _backendPriority)
                    break; // registry is sorted by descending priority

                IntPtr candidate = _registryCreate(_context, id);
                if (candidate == IntPtr.Zero)
                    continue;

                if ((_backendFeatures(candidate) & FeatureSupportedAtRuntime) == 0)
                {
                    _backendFree(candidate);
                    continue;
                }

                int result = _backendInitialize(candidate);
                if (result != PrismOk && result != PrismErrorAlreadyInitialized)
                {
                    _backendFree(candidate);
                    continue;
                }

                FreeBackend();
                Adopt(candidate);
                Main.Log.LogInfo("Prism: screen reader detected; switched to " + BackendName + ".");
                return;
            }
        }

        private static void Adopt(IntPtr backend)
        {
            _backend = backend;
            BackendName = Utf8At(_backendName(backend));
            _backendId = _registryId(_context, ToUtf8(BackendName));
            _backendPriority = _registryPriority(_context, _backendId);
            _lastProbeTick = Environment.TickCount;
        }

        private static void FreeBackend()
        {
            if (_backend == IntPtr.Zero)
                return;

            _backendFree(_backend);
            _backend = IntPtr.Zero;
            _backendId = 0;
            BackendName = string.Empty;
        }

        private static bool IsTtsFallback(ulong id)
        {
            return id == BackendSapi || id == BackendOneCore
                || id == BackendAvSpeech || id == BackendSpeechDispatcher;
        }

        private static string ErrorText(int error)
        {
            string text = Utf8At(_errorString(error));
            return text.Length > 0 ? text : "error " + error;
        }

        private static byte[] ToUtf8(string text)
        {
            return Encoding.UTF8.GetBytes(text + "\0");
        }

        /// <summary>Reads a null-terminated UTF-8 string owned by native code.</summary>
        private static string Utf8At(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
                return string.Empty;

            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
                length++;

            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
