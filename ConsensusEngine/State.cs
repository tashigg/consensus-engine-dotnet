using System;
using System.Runtime.InteropServices;

namespace Tashi.ConsensusEngine
{
    /// <summary>
    /// Application state.
    /// </summary>
    public abstract class State : IDisposable
    {
        internal IntPtr StatePtr;

        internal State()
        {
        }

        ~State()
        {
            Dispose();
        }

        internal static State FromPtr(IntPtr statePtr)
        {
            // FIXME: when other state types are added, this will need to check which type it is
            return new BlobState(statePtr);
        }

        internal void Report(Platform platform, UInt64 epochIndex)
        {
            tce_state_report(platform.handle, epochIndex, StatePtr)
                .SuccessOrThrow("tce_state_report");
        }

        public void Dispose()
        {
            if (StatePtr == IntPtr.Zero) return;

            tce_state_free(StatePtr).SuccessOrThrow("tce_state_free");
            StatePtr = IntPtr.Zero;
        }

        [DllImport("tce_ffi", EntryPoint = "tce_state_report", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_report(
            IntPtr platformPtr,
            UInt64 epochIndex,
            IntPtr statePtr
        );

        [DllImport("tce_ffi", EntryPoint = "tce_state_new_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_free(
            IntPtr statePtr
        );
    }

    // Other state types may be added in the future.
    /// <summary>
    /// Application state via a binary blob.
    /// </summary>
    public sealed class BlobState : State
    {
        public readonly UInt64 Length;

        public BlobState(ReadOnlySpan<byte> blob)
        {
            unsafe
            {
                fixed (byte* blobPtr = blob)
                {
                    tce_state_new_blob(
                            blobPtr,
                            (UInt32)blob.Length,
                            out StatePtr
                        )
                        .SuccessOrThrow("tce_state_new_blob");
                }
            }

            Length = (UInt64)blob.Length;
        }

        internal BlobState(IntPtr statePtr)
        {
            StatePtr = statePtr;

            tce_state_blob_len(statePtr, out Length)
                .SuccessOrThrow("tce_state_blob_len");
        }

        public uint ReadBytes(ulong offset, Span<byte> readBuf)
        {
            unsafe
            {
                fixed (byte* readBufPtr = readBuf)
                {
                    tce_state_read_blob(StatePtr, offset, readBufPtr, (UInt32)readBuf.Length, out var bytesRead)
                        .SuccessOrThrow("tce_state_read_blob");
                    return bytesRead;
                }
            }
        }

        [DllImport("tce_ffi", EntryPoint = "tce_state_new_blob", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern Result tce_state_new_blob(
            byte* blobPtr,
            UInt32 blobLen,
            out IntPtr stateOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_state_blob_len", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe Result tce_state_blob_len(
            IntPtr statePtr,
            out UInt64 lenOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_state_read_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe Result tce_state_read_blob(
            IntPtr statePtr,
            UInt64 offset,
            byte* blobPtr,
            UInt32 readBufLen,
            out UInt32 bytesRead
        );
    }
}