using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Tashi.ConsensusEngine
{
    /// <summary>
    /// Log sink for native loggable events.
    /// </summary>
    public static class Logger
    {
        public delegate void LogDelegate(string message);

        private static LogDelegateHolder? _logDelegateHolder;

        public static void Init(LogDelegate log, LogDelegate logWarning, LogDelegate logError)
        {
            // Important: if there's an existing delegate holder, tell it not to clear the log functions
            // in its finalizer as we're going to be replacing them anyway.
            //
            // Otherwise, when the GC finalizes the object sometime after this method returns,
            // it will clear the log functions we just set.
            _logDelegateHolder?.Forget();

            unsafe
            {
                // SAFETY: we use a class that implements `IDisposable` to ensure we clear the log function pointers
                // before they are invalidated. This generally only matters when running the application in Unity,
                // as it may load and unload this class every time the application is run within the editor,
                // but the TCE dynamic library will remain loaded in the process and so may try to invoke a function
                // pointer from a previous run, which will immediately segfault or worse.
                _logDelegateHolder = new LogDelegateHolder(log, logWarning, logError);
            }
        }

        public static void SetFilter(string logFilter)
        {
            tce_log_set_filter(logFilter).SuccessOrThrow("tce_log_set_filter");
        }

        private static unsafe string DecodeUtf8String(byte* pointer, UIntPtr len)
        {
            try
            {
                checked
                {
                    // SAFETY: this interface requires us to cast `len` to `int`, but apparently casting
                    // `UIntPtr` to `int` has no interaction with the `checked {}` block.
                    //
                    // However, converting to `UInt64` first does, likely because it returns a primitive `ulong`.
                    return Encoding.UTF8.GetString(pointer, (int)len.ToUInt64());
                }
            }
            catch (OverflowException)
            {
                return "(oversized message)";
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void NativeLogDelegate(byte* utf8String, UIntPtr len);

        [DllImport("tce_ffi", EntryPoint = "tce_log_set_functions", CallingConvention = CallingConvention.Cdecl)]
        static extern Result tce_log_set_functions
        (
            NativeLogDelegate? log,
            NativeLogDelegate? logWarning,
            NativeLogDelegate? logError
        );

        [DllImport("tce_ffi", EntryPoint = "tce_log_set_filter", CallingConvention = CallingConvention.Cdecl)]
        static extern Result tce_log_set_filter
        (
            [MarshalAs(UnmanagedType.LPUTF8Str)] string logFilter
        );

        private class LogDelegateHolder : IDisposable
        {
            // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
            private readonly NativeLogDelegate _log;
            private readonly NativeLogDelegate _logWarning;
            private readonly NativeLogDelegate _logError;
            // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

            private bool _clearOnDispose = true;

            public LogDelegateHolder(LogDelegate log, LogDelegate logWarning, LogDelegate logError)
            {
                unsafe
                {
                    _log = (pointer, len) => log(DecodeUtf8String(pointer, len));
                    _logWarning = (pointer, len) => logWarning(DecodeUtf8String(pointer, len));
                    _logError = (pointer, len) => logError(DecodeUtf8String(pointer, len));
                }

                tce_log_set_functions(_log, _logWarning, _logError);
            }

            public void Forget()
            {
                _clearOnDispose = false;
            }

            public void Dispose()
            {
                // `.Dispose()` may be called multiple times but it must be idempotent.
                if (_clearOnDispose)
                {
                    // SAFETY: we must clear the function pointers in TCE before they are invalidated.
                    // This is thread-safe.
                    tce_log_set_functions(null, null, null);

                    _clearOnDispose = false;
                }
            }
        }
    }
}
