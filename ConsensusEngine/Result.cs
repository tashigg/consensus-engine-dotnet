using System;

namespace Tashi.ConsensusEngine
{
    /// <summary>
    /// The result of a native operation.
    /// </summary>
    public enum Result
    {
        Success,
        LogFilePathError,
        ParseAddressError,
        BindListenerError,
        NotInited,
        NotStarted,
        RuntimeCreationError,
        SecretKeyConstructionError,
        PlatformCreationError,
        BufferTooSmall,
        ConversionToDerError,
        ConversionFromDerError,
        SpawnUdpError,
        SendError,
        DataTooLarge,
        CreatorIdTooLarge,
        CreatorIdMissing,
        ReceiveEventError,
        InitialNodesMissing,
        AlreadyStarted,
        EmptyAddressBook,
        FailedToDetermineLocalIp,
        TransmitQueueEmpty,
        ArgumentError,
        ExternalModeRequired,
        RecvNotReady,
        InvalidSockAddr,
        BindAddressRequired,
        LoggingAlreadySet,
        RelaySessionError,
        RelaySessionInitIncomplete,
        RelaySessionRateLimited,
        RelaySessionUserLimitExceeded,
        RelaySessionIntervalOutOfRange,
        EncounteredSyncPoint,

        // State-specific result codes
        /// <summary>
        /// Attempted to use a blob specific state function on non-blob state.
        /// </summary>
        NotABlob,
        WrongProgressKind,
        SendWouldBlock,
    }

    public static class ResultExtensions
    {
        public static void SuccessOrThrow(this Result result, string operationName)
        {
            if (result != Result.Success)
            {
                throw new ResultException(result, operationName);
            }
        }
    }

    public sealed class ResultException : Exception
    {
        public readonly Result Result;

        internal ResultException(Result result, string operationName) : base($"error from {operationName}: {result}")
        {
            Result = result;
        }
    }
}
