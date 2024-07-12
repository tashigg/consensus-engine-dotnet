using System;
using System.Runtime.InteropServices;

namespace Tashi.ConsensusEngine
{
    public class StateRequest : IDisposable
    {
        private IntPtr _requestPtr;

        internal StateRequest(Platform platform, UInt64 epochIndex)
        {
            tce_state_request(platform.handle, epochIndex, out _requestPtr)
                .SuccessOrThrow("tce_state_request");
        }

        ~StateRequest()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_requestPtr == IntPtr.Zero) return;

            tce_state_request_free(_requestPtr)
                .SuccessOrThrow("tce_state_request_free");
            _requestPtr = IntPtr.Zero;
        }

        public ProgressKind UpdateProgress()
        {
            tce_state_request_update_progress(_requestPtr, out var progressKind)
                .SuccessOrThrow("tce_state_request_update_progress");

            return progressKind;
        }

        /// <summary>
        /// If <see cref="UpdateProgress"/> last returned <see cref="F:ProgressKind.Confirming"/>,
        /// this will return the details for that step.
        /// </summary>
        /// <exception cref="ResultException"></exception>
        public ConfirmingProgress? Confirming
        {
            get
            {
                return tce_state_request_confirming_progress(_requestPtr, out var progress)
                    switch
                {
                    Result.Success => progress,
                    Result.WrongProgressKind => null,
                    var other => throw new ResultException(other, "tce_state_request_confirming_progress")
                };
            }
        }

        /// <summary>
        /// If <see cref="UpdateProgress"/> last returned <see cref="F:ProgressKind.Downloading"/>,
        /// this will return the details for that step.
        /// </summary>
        /// <exception cref="ResultException"></exception>
        public DownloadingProgress? Downloading
        {
            get
            {
                return tce_state_request_downloading_progress(_requestPtr, out var progress)
                    switch
                {
                    Result.Success => progress,
                    Result.WrongProgressKind => null,
                    var other => throw new ResultException(other, "tce_state_request_confirming_progress")
                };
            }
        }

        /// <summary>
        /// If <see cref="UpdateProgress"/> last returned <see cref="F:ProgressKind.Ready"/>,
        /// this will return the details for that step.
        /// </summary>
        /// <exception cref="ResultException"></exception>
        public State? Ready
        {
            get
            {
                return tce_state_request_ready(_requestPtr, out var statePtrOut)
                    switch
                {
                    Result.Success => State.FromPtr(statePtrOut),
                    Result.WrongProgressKind => null,
                    var other => throw new ResultException(other, "tce_state_request_confirming_progress")
                };
            }
        }

        public struct Progress
        {
            public ulong Current;
            public ulong Target;
        }

        public struct ConfirmingProgress
        {
            /**
             * The current progress in the number of state proofs received for a candidate state.
             *
             * If multiple candidates exist, this represents the one with the greatest number of confirmations so far.
             *
             * A super-majority (2/3 + 1) of active peers (not including relays) must submit state proofs for a state
             * to be considered authoritative.
             */
            public Progress Confirmations;
        }

        public struct DownloadingProgress
        {
            /**
             * The download progress represented as number of unique chunks of state (1 - 16 KiB each).
             *
             * By the nature of the indexing process, chunks are deduplicated, so depending on the amount of redundancy
             * in the application state, this may be much smaller than the total size of the state.
             */
            public Progress Chunks;

            /**
             * The download progress represented as the aggregate number of bytes to download.
             *
             * This is the sum of the sizes of `chunks`.
             *
             * By the nature of the indexing process, chunks are deduplicated, so depending on the amount of redundancy
             * in the application state, this may be much smaller than the total size of the state.
             */
            public Progress Bytes;
        }

        public enum ProgressKind : byte
        {
            /**
            * The authoritative state for the requested epoch is still being confirmed.
            *
            * See <see cref="F:StateRequest.Confirming"/> for details.
            */
            Confirming,

            /**
             * An authoritative state for the requested epoch has been selected.
             */
            Confirmed,

            /**
             * The selected state for the requested epoch is in the process of being downloaded.
             *
             *
            * See <see cref="F:StateRequest.Downloading"/> for details.
             */
            Downloading,

            /**
             * The requested state for the given epoch is ready. Enjoy!
             *
             * See <see cref="F:StateRequest.Ready"/> for the completed state.
             *
             * This is a terminal state: no more progress will be sent.
             * Future calls to the same `StateRequest` will return `Cancelled`.
             */
            Ready,

            /**
             * The submitted state report for the requested epoch matches the authoritative state hash for the network.
             *
             * It is safe to use the existing state.
             *
             * This is a terminal state: no more progress will be sent.
             * Future calls to the same `StateRequest` will return `Cancelled`.
             */
            Converged,

            /**
             * The `StateRequest` was previously cancelled.
             *
             * The most likely scenario for this result is the request being overridden by one for a later epoch.
             *
             * This is also the value returned if a terminal state was previously received.
             *
             * This is a terminal state: no more progress will be sent.
             * Future calls to the same `StateRequest` will return `Cancelled`.
             */
            Cancelled,

            /**
             * The requested epoch is now too far in the past for peers to still have its state cached.
             *
             * The application should return to the message stream and discard all events until the next sync point,
             * at which point it should request the state for the new epoch.
             *
             * If any chunks of state were successfully downloaded, they will remain in-cache until the new request
             * finds an authoritative state.
             *
             * Ideally, the new state shares some chunks with the previously requested state and those are the chunks
             * we managed to download already, so we can continue to make progress.
             *
             * If multiple epochs pass without being able to make any progress, it is likely that the state is changing
             * too much to download in a timely fashion at the user's current network speed, which unfortunately means they
             * won't be able to participate.
             *
             * Applying compression to the state may help alleviate this problem,
             * which Tashi may do transparently in the future.
             *
             * This is a terminal state: no more progress will be sent.
             * Future calls to the same `StateRequest` will return `Cancelled`.
             */
            TimedOut,

            /**
             * An unrecoverable error occurred while fetching the state.
             *
             * This is a terminal state: no more progress will be sent.
             * Future calls to the same `StateRequest` will return `Cancelled`.
             */
            Error,
        }

        [DllImport("tce_ffi", EntryPoint = "tce_state_request", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_request(
                IntPtr platformPtr,
                UInt64 epochIndex,
                out IntPtr requestPtrOut
            );

        [DllImport("tce_ffi", EntryPoint = "tce_state_request_update_progress", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_request_update_progress(
            IntPtr requestPtr,
            out ProgressKind progressKindOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_state_request_confirming_progress", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_request_confirming_progress(
            IntPtr requestPtr,
            out ConfirmingProgress progressOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_state_request_downloading_progress", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_request_downloading_progress(
            IntPtr requestPtr,
            out DownloadingProgress progressOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_state_request_ready", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_request_ready(
            IntPtr requestPtr,
            out IntPtr statePtrOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_state_request_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_state_request_free(IntPtr requestPtr);
    }
}