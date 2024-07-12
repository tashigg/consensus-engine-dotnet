using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// TODO: is joining an existing session with a populated address book valid?

namespace Tashi.ConsensusEngine
{
    public sealed class PlatformConfig
    {
        public const UInt64 DefaultBaseMinEventIntervalMicros = 1_500;
        public const Int64 DefaultFallenBehindKickSeconds = 10;
        public const UInt64 DefaultHeartbeatMicros = 500_000;
        public const UInt32 DefaultTargetAckLatencyMillis = 400;
        public const UInt32 DefaultMaxAckLatencyMillis = 600;
        public const UInt32 DefaultThrottleAckLatencyMillis = 900;
        public const UInt32 DefaultResetAckLatencyMillis = 2000;
        public const UInt32 DefaultTransactionChannelSize = 32;
        public const UInt32 DefaultMaxUnacknowledgedBytes = 1024 * 1024 * 500;
        public const Int32 DefaultMaxActiveRelaysPerDestination = 2;
        public const UInt32 DefaultMaxBlockingVerifyThreads = 0;
        public const UInt16 DefaultEpochStatesToCache = 3;

        /// <summary>
        /// The address to bind to, e.g. `new IPEndPoint(IPAddress.Any, 0)`.
        /// </summary>
        public IPEndPoint BindAddress { get; set; }

        public SecretKey SecretKey { get; set; }


        /// <summary>
        /// With ideal network conditions (such as loopback) or large networks,
        /// the consensus engine may put an unreasonable burden on the CPU if
        /// this setting is too low.
        ///
        /// 1,500 microseconds is a reasonable default.
        ///
        /// This setting should ideally be the same for all members of a network,
        /// so this should not be exposed to the end user.
        ///
        /// The CPU burden created by a network scales more-or-less linearly with the
        /// number of nodes in the network, so the min event interval also scales linearly.
        ///
        /// For example, if this is set to 1,000 micros,and there are 8 nodes in the network,
        /// the effective min event interval will be 8,000 micros.
        /// </summary>
        public UInt64 BaseMinEventIntervalMicros { get; set; } = DefaultBaseMinEventIntervalMicros;

        /// <summary>
        /// If a creator has been inactive for this duration, we will vote to kick them.
        /// Negative value means we will never vote to kick.
        ///
        /// Defaults to 10 seconds
        /// </summary>
        public Int64 FallenBehindKickSeconds { get; set; } = DefaultFallenBehindKickSeconds;


        /// <summary>
        /// When there is no data to finalize, the consensus engine will create
        /// empty events at this interval to keep the session alive.
        /// </summary>
        public UInt64 HeartbeatMicros { get; set; } = DefaultHeartbeatMicros;



        /// <summary>
        /// As throughput across the session increases, the ack latency increases.
        /// When the ack latency rises above this threshold, we vote that throughput
        /// across the session should not increase further.
        ///
        /// If this threshold is lower than our uncongested ping, then we'll erroneously always
        /// vote to restrict throughput.
        ///
        /// Defaults to 400 ms
        /// </summary>
        public UInt32 TargetAckLatencyMillis { get; set; } = DefaultTargetAckLatencyMillis;

        /// <summary>
        /// If ack latency rises above this threshold, we vote to gradually reduce throughput
        /// across the session to bring it down.
        ///
        /// Defaults to 600 ms
        /// </summary>
        public UInt32 MaxAckLatencyMillis { get; set; } = DefaultMaxAckLatencyMillis;

        /// <summary>
        /// If ack latency rises above this threshold, we vote to drastically restrict throughput
        /// across the session as an emergency measure.
        ///
        /// Defaults to 900 ms.
        /// </summary>
        public UInt32 ThrottleAckLatencyMillis { get; set; } = DefaultThrottleAckLatencyMillis;

        /// <summary>
        /// If ack latency rises above this threshold, we vote to reset throughput restriction to its initial value.
        /// This is a last-ditch effort to recover from rising ack latency.
        ///
        /// Defaults to 2000 ms
        /// </summary>
        public UInt32 ResetAckLatencyMillis { get; set; } = DefaultResetAckLatencyMillis;

        /// <summary>
        /// If `true`, we will vote to resize the epoch depending on network conditions.
        /// 
        /// Defaults to `true`.
        ///
        /// Depending on network conditions, rounds may pass more quickly or more slowly.
        ///
        /// Whenever a creator joins or leaves, they'll have to wait out the epoch before the
        /// address book change takes effect.
        ///
        /// A leaving creator doesn't want to wait too long, and a joining creator needs a sufficiently long window in which to join.
        ///
        /// Creators who don't disable this config option will automatically vote to keep epoch lengths in the range of 1 to 3 seconds.
        /// </summary>
        public Boolean EnableDynamicEpochSize { get; set; } = true;

        /// <summary>
        /// The maximum number of transactions to buffer before applying backpressure.
        ///
        /// Defaults to 32.
        /// </summary>
        public UIntPtr TransactionChannelSize { get; set; } = (UIntPtr)DefaultTransactionChannelSize;

        /// <summary>
        /// How many bytes worth of transactions that haven't yet been seen by the network to pull from the transaction buffer.
        ///
        /// Defaults to `500 MiB` (`1024 * 1024 * 500 bytes`).
        /// </summary>
        public UIntPtr MaxUnacknowledgedBytes { get; set; } = (UIntPtr)DefaultMaxUnacknowledgedBytes;

        /// <summary>
        /// The maximum number of active relays to support per requestor.
        ///
        /// A negative value means no limit.
        ///
        /// If a node receives an event and does not recognize one or more of its other-parent hashes,
        /// it will request those parent events from the peer that sent the descendant event;
        /// that peer will then assume that the node does not have an active connection to the creator
        /// of those parent events and begin relaying new events from that creator without needing
        /// to be asked.
        ///
        /// This value limits the number of peers we will relay to at any given time.
        /// </summary>
        public IntPtr MaxActiveRelaysPerDestination { get; set; } = (IntPtr)DefaultMaxActiveRelaysPerDestination;

        /// <summary>
        /// Above a constant threshold, signature verifications are sent to a blocking thread pool
        /// instead of using spare compute time in Tokio's core thread pool.
        ///
        /// This sets the maximum number of threads to spawn for blocking verifications.
        ///
        /// It cannot be zero or else events that grow larger than the threshold cannot be verified.
        ///
        /// If set to zero, or left at default, it will interally be set to:
        /// * The number of CPU cores reported by the system minus one, if the result is not zero, or:
        /// * One.
        /// </summary>
        public UIntPtr MaxBlockingVerifyThreads { get; set; } = (UIntPtr)DefaultMaxBlockingVerifyThreads;

        /// <summary>
        /// State sharing won't work unless this is `true`.
        ///
        /// Defaults to `false`.
        /// </summary>
        public Boolean EnableStateSharing { get; set; } = false;

        /// <summary>
        /// When state sharing is enabled, this is the number of epoch states to cache.
        ///
        /// If a fallen behind creator fails to download an epoch's state in time, they will have to restart the download.
        ///
        /// Defaults to 3.
        /// </summary>
        public UInt16 EpochStatesToCache { get; set; } = DefaultEpochStatesToCache;

        /// <summary>
        /// If `true`:
        /// * When we receive a hole-punch request from a peer, we will try to coordinate
        ///   a hole-punch between that peer and the peer they wish to hole-punch to.
        /// * When we fail to directly connect to a peer, we will send hole-punch requests
        ///   to any peers we are connected to.
        ///
        /// Defaults to `true`.
        /// </summary>
        public Boolean EnableHolePunching { get; set; } = true;



        public PlatformConfig(IPEndPoint bindAddress, SecretKey secretKey)
        {
            BindAddress = bindAddress;
            SecretKey = secretKey;
        }

        public PlatformConfig(IPEndPoint bindAddress) : this(bindAddress, SecretKey.Generate())
        {
        }
    }

    public abstract class Platform : IDisposable
    {
        // Unity Relay enforces a limit of 1400 bytes per each datagram.
        public const UInt64 MaxRelayDataLen = 1400;


        // NOTE: this must match `TceNetworkMode` in TCE or things will get weird.
        protected enum NetworkMode
        {
            Loopback,
            Local,
            External
        }

        public PublicKey PublicKey => _secretKey.PublicKey;

        private readonly SecretKey _secretKey;
        internal IntPtr handle;
        protected bool _started;
        private bool _disposed;

        /// <summary>
        /// Create a new platform. It won't begin communicating with other nodes
        /// until the Start method is called.
        /// </summary>
        protected Platform(PlatformConfig config, NetworkMode mode)
        {
            _secretKey = config.SecretKey;

            handle = tce_init(
                mode,
                config.BindAddress.Address.ToString(),
                (ushort)config.BindAddress.Port,
                config.BaseMinEventIntervalMicros,
                _secretKey.Der,
                (uint)_secretKey.Der.Length,
                out var result
            );

            result.SuccessOrThrow("tce_init");

            if (config.FallenBehindKickSeconds != PlatformConfig.DefaultFallenBehindKickSeconds)
            {
                tce_set_fallen_behind_kick_seconds(handle, config.FallenBehindKickSeconds)
                    .SuccessOrThrow("tce_set_fallen_behind_kick_seconds");
            }

            if (config.HeartbeatMicros != PlatformConfig.DefaultHeartbeatMicros)
            {
                tce_set_heartbeat_micros(handle, config.HeartbeatMicros)
                    .SuccessOrThrow("tce_set_heartbeat_micros");
            }

            if (config.TargetAckLatencyMillis != PlatformConfig.DefaultTargetAckLatencyMillis)
            {
                tce_set_target_ack_latency_millis(handle, config.TargetAckLatencyMillis)
                    .SuccessOrThrow("tce_set_target_ack_latency_millis");
            }

            if (config.MaxAckLatencyMillis != PlatformConfig.DefaultTargetAckLatencyMillis)
            {
                tce_set_max_ack_latency_millis(handle, config.MaxAckLatencyMillis)
                    .SuccessOrThrow("tce_set_max_ack_latency_millis");
            }

            if (config.ThrottleAckLatencyMillis != PlatformConfig.DefaultTargetAckLatencyMillis)
            {
                tce_set_throttle_ack_latency_millis(handle, config.ThrottleAckLatencyMillis)
                    .SuccessOrThrow("tce_set_throttle_ack_latency_millis");
            }

            if (config.ResetAckLatencyMillis != PlatformConfig.DefaultTargetAckLatencyMillis)
            {
                tce_set_reset_ack_latency_millis(handle, config.ResetAckLatencyMillis)
                    .SuccessOrThrow("tce_set_reset_ack_latency_millis");
            }

            if (config.EnableDynamicEpochSize == false)
            {
                tce_set_enable_dynamic_epoch_size(handle, false)
                    .SuccessOrThrow("tce_set_enable_dynamic_epoch_size");
            }

            if (config.TransactionChannelSize != (UIntPtr)PlatformConfig.DefaultTransactionChannelSize)
            {
                tce_set_transaction_channel_size(handle, config.TransactionChannelSize)
                    .SuccessOrThrow("tce_set_transaction_channel_size");
            }

            if (config.MaxUnacknowledgedBytes != (UIntPtr)PlatformConfig.DefaultMaxUnacknowledgedBytes)
            {
                tce_set_max_unacknowledged_bytes(handle, config.MaxUnacknowledgedBytes)
                    .SuccessOrThrow("tce_set_max_unacknowledged_bytes");
            }

            if (config.MaxActiveRelaysPerDestination != (IntPtr)PlatformConfig.DefaultMaxActiveRelaysPerDestination)
            {
                tce_set_max_active_relays_per_destination(handle, config.MaxActiveRelaysPerDestination)
                    .SuccessOrThrow("tce_set_max_active_relays_per_destination");
            }

            if (config.MaxBlockingVerifyThreads != (UIntPtr)PlatformConfig.DefaultMaxBlockingVerifyThreads)
            {
                tce_set_max_blocking_verify_threads(handle, config.MaxBlockingVerifyThreads)
                    .SuccessOrThrow("tce_set_max_blocking_verify_threads");
            }

            if (config.EnableStateSharing)
            {
                tce_set_enable_state_sharing(handle, true)
                    .SuccessOrThrow("tce_set_enable_state_sharing");
            }

            if (config.EpochStatesToCache != PlatformConfig.DefaultEpochStatesToCache)
            {
                tce_set_epoch_states_to_cache(handle, config.EpochStatesToCache)
                    .SuccessOrThrow("tce_set_epoch_states_to_cache");
            }

            if (config.EnableHolePunching == false)
            {
                tce_set_enable_hole_punching(handle, false)
                    .SuccessOrThrow("tce_set_enable_hole_punching");
            }
        }

        /// <summary>
        /// If a port wasn't specified, or if port 0 was specified then this
        /// function will help in determining which port was actually used.
        /// </summary>
        public IPEndPoint GetBoundAddress()
        {
            var buffer = new StringBuilder();
            var requiredSize = tce_bound_address_get(handle, buffer, buffer.Capacity, out var port);
            if (requiredSize < 0)
            {
                throw new SystemException($"Failed to get the bound address: {requiredSize}");
            }
            else if (requiredSize > buffer.Capacity)
            {
                buffer.Capacity = requiredSize;
                if (requiredSize != tce_bound_address_get(handle, buffer, buffer.Capacity, out port))
                {
                    throw new SystemException($"Failed to get the bound address: {requiredSize}");
                }
            }

            buffer.Length = requiredSize - 1;

            return new IPEndPoint(
                IPAddress.Parse(buffer.ToString()),
                port
            );
        }

        protected void SetAddressBook(IEnumerable<AddressBookEntry>? entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                AddAddressBookEntry(entry);
            }
        }

        protected void AddAddressBookEntry(AddressBookEntry entry)
        {
            string address;

            if (entry is DirectAddressBookEntry direct)
            {
                address = direct.EndPoint.ToString();
            }
            else if (entry is ExternalAddressBookEntry external)
            {
                address = external.PublicKey.SyntheticEndpoint.ToString();
            }
            else
            {
                throw new ArgumentException($"Unsupported AddressBookEntry type: {entry}");
            }

            tce_add_node(
                    handle,
                    address,
                    entry.PublicKey.Der,
                    (uint)entry.PublicKey.Der.Length,
                    entry.IsRelay
                )
                .SuccessOrThrow("tce_add_node");
        }

        /// <summary>
        /// Votes to add the node to the running session. If a super majority (> 2/3) of
        /// nodes vote to add the same node then it will pass.
        ///
        /// Any pending votes for the same target will be overwritten.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="timeout"></param>
        /// <seealso cref="VoteToRemoveNode"/>
        /// <seealso cref="CancelPendingVote"/>
        public void VoteToAddNode(AddressBookEntry entry, TimeSpan timeout)
        {
            string address;

            if (entry is DirectAddressBookEntry direct)
            {
                address = direct.EndPoint.ToString();
            }
            else if (entry is ExternalAddressBookEntry external)
            {
                address = external.PublicKey.SyntheticEndpoint.ToString();
            }
            else
            {
                throw new ArgumentException($"Unsupported AddressBookEntry type: {entry}");
            }

            tce_vote_add_node(handle,
                entry.PublicKey.Der,
                (uint)entry.PublicKey.Der.Length,
                address,
                (ulong)(timeout.TotalMilliseconds * 1_000_000)
            ).SuccessOrThrow("tce_vote_add_node");
        }

        /// <summary>
        /// Votes to remove the node from the running session. If a super majority (> 2/3) of
        /// nodes vote to remove the same node then it will pass.
        ///
        /// Any pending votes for the same target will be overwritten.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="timeout"></param>
        /// <seealso cref="VoteToAddNode"/>
        /// <seealso cref="CancelPendingVote"/>
        public void VoteToRemoveNode(PublicKey target, TimeSpan timeout)
        {
            tce_vote_remove_node(handle,
                target.Der,
                (uint)target.Der.Length,
                (ulong)(timeout.TotalMilliseconds * 1_000_000)
            ).SuccessOrThrow("tce_vote_remove_node");
        }

        /// <summary>
        /// Cancels the pending vote to add or remove the target.
        /// </summary>
        /// <param name="target"></param>
        public void CancelPendingVote(PublicKey target)
        {
            tce_vote_cancel(handle,
                target.Der,
                (uint)target.Der.Length
            ).SuccessOrThrow("tce_vote_cancel");
        }

        /// <summary>
        /// Votes to end the session. If a super majority (> 2/3) of
        /// nodes vote to end the session then it will pass.
        /// </summary>
        /// <seealso cref="CancelVoteEndSession"/>
        public void VoteEndSession()
        {
            tce_vote_end_session(handle)
                .SuccessOrThrow("tce_vote_end_session");
        }

        public void CancelVoteEndSession()
        {
            tce_vote_end_session_cancel(handle)
                .SuccessOrThrow("tce_vote_end_session_cancel");
        }

        /// <summary>
        /// Starts the session.
        /// </summary>
        /// <param name="joiningRunningSession">
        /// Whether the session will be newly created with pre-determined peers,
        /// or an already running session.
        /// </param>
        /// <param name="addressBook"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <seealso cref="VoteToAddNode"/>
        public void Start(IEnumerable<AddressBookEntry>? addressBook, bool joiningRunningSession,
            Guid? relaySessionId = null)
        {
            if (_started)
            {
                throw new InvalidOperationException("The platform has already been started");
            }

            SetAddressBook(addressBook);

            byte[] bigEndianSessionId = relaySessionId?.ToBigEndianBytes() ?? new byte[] { };
            tce_start(
                handle,
                joiningRunningSession,
                bigEndianSessionId,
                (uint)bigEndianSessionId.Length
            ).SuccessOrThrow("tce_start");

            _started = true;
        }

        /// <summary>
        /// Gets an event or a sync point if either is available.
        /// </summary>
        /// <returns>
        /// If an event is available then a <see cref="ConsensusEvent"/> will be returned.
        /// If a sync point has been reached, then a <see cref="ConsensusSyncPoint"/> will be returned.
        /// Otherwise, if no event is available then `null` will be returned.
        /// </returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ResultException"></exception>
        public Event? GetEvent()
        {
            if (!_started)
            {
                throw new InvalidOperationException("The platform hasn't been started");
            }

            var eventPointer = tce_event_get(handle, out var result);

            switch (result)
            {
                case Result.Success:
                    {
                        if (eventPointer == IntPtr.Zero)
                        {
                            return null;
                        }

                        return new Event(new ConsensusEvent(eventPointer));
                    }

                case Result.EncounteredSyncPoint:
                    {
                        var syncPointPtr = tce_sync_point_get(handle, out var syncPointResult);
                        syncPointResult.SuccessOrThrow("tce_sync_point_get");
                        var consensusSyncPoint =
                            new ConsensusSyncPoint(Marshal.PtrToStructure<NativeConsensusSyncPoint>(syncPointPtr));
                        tce_sync_point_free(syncPointPtr);
                        return new Event(consensusSyncPoint);
                    }

                default:
                    throw new ResultException(result, "tce_event_get");
            }
        }

        /// <summary>
        /// Resets the message stream.
        ///
        /// Last-ditch recovery method if you've fallen behind and `request_state` is failing.
        /// </summary>
        /// <exception cref="ResultException"></exception>
        public void ResetMessageStream()
        {
            if (!_started)
            {
                throw new InvalidOperationException("The platform hasn't been started");
            }

            var result = tce_reset_message_stream(handle);

            if (result != Result.Success)
            {
                throw new ResultException(result, "tce_reset_message_stream");
            }
        }

        /// <summary>
        /// Attempts to queue data to be sent. It will fail if the queue is
        /// full.
        /// </summary>
        /// <seealso cref="SendBlocking"/>
        /// <param name="data"></param>
        /// <returns>`true` if the data was queued.</returns>
        /// <exception cref="ResultException">If an unexpected error occurs.</exception>
        public bool TrySend(ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* bytes = data)
                {
                    var result = tce_try_send(handle, bytes, (UInt32)data.Length);
                    switch (result)
                    {
                        case Result.Success:
                            return true;

                        case Result.SendWouldBlock:
                            return false;

                        default:
                            throw new ResultException(result, "tce_try_send");
                    }
                }
            }
        }

        /// <summary>
        /// Queues the data for sending. If the queue is full then the function
        /// will block until space becomes available.
        ///
        /// When the queue is full the order of attempted sends is preserved as
        /// space becomes available.
        /// </summary>
        /// <seealso cref="TrySend"/>
        /// <param name="data"></param>
        /// <exception cref="ResultException">If an unexpected error occurs.</exception>
        public void SendBlocking(ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* bytes = data)
                {
                    tce_send_blocking(handle, bytes, (UInt32)data.Length).SuccessOrThrow("tce_send_blocking");
                }
            }
        }


        /// <summary>
        /// 
        /// Sends a plugin transaction to the pool. 
        ///
        /// </summary>
        /// <seealso cref="TrySend"/>
        /// <param name="data"></param>
        /// <exception cref="ResultException">If an unexpected error occurs.</exception>
        public void PluginSendBlocking(ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* bytes = data)
                {
                    tce_plugin_send(handle, bytes, (UInt32)data.Length).SuccessOrThrow("tce_plugin_send");
                }
            }
        }

        public StateRequest RequestState(UInt64 epochIndex)
        {
            return new StateRequest(this, epochIndex);
        }

        public void ReportState(UInt64 epochIndex, State state)
        {
            state.Report(this, epochIndex);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of owned, managed resources
            }

            if (handle != IntPtr.Zero)
            {
                tce_free(handle);
                handle = IntPtr.Zero;
            }

            _disposed = true;
        }

        ~Platform()
        {
            Dispose(false);
        }

        [DllImport("tce_ffi", EntryPoint = "tce_init", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tce_init(
            NetworkMode mode,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? address,
            UInt16 port,
            UInt64 baseMinEventIntervalMicros,
            byte[] publicKeyDer,
            UInt32 publicKeyDerLen,
            out Result result
        );

        [DllImport("tce_ffi", EntryPoint = "tce_set_fallen_behind_kick_seconds",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_fallen_behind_kick_seconds(IntPtr platform, Int64 seconds);

        [DllImport("tce_ffi", EntryPoint = "tce_set_heartbeat_micros", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_heartbeat_micros(IntPtr platform, UInt64 micros);

        [DllImport("tce_ffi", EntryPoint = "tce_set_target_ack_latency_millis", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_target_ack_latency_millis(IntPtr platform, UInt32 millis);

        [DllImport("tce_ffi", EntryPoint = "tce_set_max_ack_latency_millis", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_max_ack_latency_millis(IntPtr platform, UInt32 millis);

        [DllImport("tce_ffi", EntryPoint = "tce_set_throttle_ack_latency_millis", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_throttle_ack_latency_millis(IntPtr platform, UInt32 millis);

        [DllImport("tce_ffi", EntryPoint = "tce_set_reset_ack_latency_millis", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_reset_ack_latency_millis(IntPtr platform, UInt32 millis);

        [DllImport("tce_ffi", EntryPoint = "tce_set_enable_dynamic_epoch_size", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_enable_dynamic_epoch_size(IntPtr platform, Boolean enabled);

        [DllImport("tce_ffi", EntryPoint = "tce_set_transaction_channel_size", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_transaction_channel_size(IntPtr platform, UIntPtr value);

        [DllImport("tce_ffi", EntryPoint = "tce_set_max_unacknowledged_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_max_unacknowledged_bytes(IntPtr platform, UIntPtr value);

        [DllImport("tce_ffi", EntryPoint = "tce_set_max_active_relays_per_destination", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_max_active_relays_per_destination(IntPtr platform, IntPtr value);

        [DllImport("tce_ffi", EntryPoint = "tce_set_max_blocking_verify_threads", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_max_blocking_verify_threads(IntPtr platform, UIntPtr value);

        [DllImport("tce_ffi", EntryPoint = "tce_set_enable_state_sharing", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_enable_state_sharing(IntPtr platform, Boolean enabled);

        [DllImport("tce_ffi", EntryPoint = "tce_set_epoch_states_to_cache", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_epoch_states_to_cache(IntPtr platform, UInt16 value);

        [DllImport("tce_ffi", EntryPoint = "tce_set_enable_hole_punching", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_set_enable_hole_punching(IntPtr platform, Boolean enabled);

        [DllImport("tce_ffi", EntryPoint = "tce_bound_address_get",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Int32 tce_bound_address_get(
            IntPtr platform,
            [MarshalAs(UnmanagedType.LPUTF8Str)] StringBuilder buffer,
            Int32 bufferLen,
            out UInt16 port
        );

        [DllImport("tce_ffi", EntryPoint = "tce_add_node", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_add_node(
            IntPtr platform,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string address,
            byte[] publicKeyDer,
            UInt32 publicKeyDerLen,
            bool isRelay
        );

        [DllImport("tce_ffi", EntryPoint = "tce_start", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_start(
            IntPtr platform,
            bool joiningRunningSession,
            byte[] relaySessionId,
            UInt32 relaySessionIdLen
        );

        [DllImport("tce_ffi", EntryPoint = "tce_event_get", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tce_event_get(
            IntPtr platform,
            out Result result
        );

        [DllImport("tce_ffi", EntryPoint = "tce_sync_point_get", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tce_sync_point_get(
            IntPtr platform,
            out Result result
        );

        [DllImport("tce_ffi", EntryPoint = "tce_sync_point_free",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void tce_sync_point_free(IntPtr consensusSyncPoint);

        [DllImport("tce_ffi", EntryPoint = "tce_try_send", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe Result tce_try_send(
            IntPtr platform,
            byte* data,
            UInt32 dataLen
        );

        [DllImport("tce_ffi", EntryPoint = "tce_reset_message_stream", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe Result tce_reset_message_stream(IntPtr platform);

        [DllImport("tce_ffi", EntryPoint = "tce_send_blocking", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe Result tce_send_blocking(
            IntPtr platform,
            byte* data,
            UInt32 dataLen
        );

        [DllImport("tce_ffi", EntryPoint = "tce_plugin_send", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe Result tce_plugin_send(
            IntPtr platform,
            byte* data,
            UInt32 dataLen
        );

        [DllImport("tce_ffi", EntryPoint = "tce_vote_add_node", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_vote_add_node(
            IntPtr platform,
            byte[] publicKeyDer,
            UInt32 publicKeyDerLen,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string address,
            UInt64 timeoutNanos
        );

        [DllImport("tce_ffi", EntryPoint = "tce_vote_remove_node", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_vote_remove_node(
            IntPtr platform,
            byte[] publicKeyDer,
            UInt32 publicKeyDerLen,
            UInt64 timeoutNanos
        );

        [DllImport("tce_ffi", EntryPoint = "tce_vote_cancel", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_vote_cancel(
            IntPtr platform,
            byte[] publicKeyDer,
            UInt32 publicKeyDerLen
        );

        [DllImport("tce_ffi", EntryPoint = "tce_vote_end_session", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_vote_end_session(IntPtr platform);

        [DllImport("tce_ffi", EntryPoint = "tce_vote_end_session_cancel", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_vote_end_session_cancel(IntPtr platform);

        [DllImport("tce_ffi", EntryPoint = "tce_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void tce_free(IntPtr platform);
    }

    /// <summary>
    /// This is intended for use with sessions that don't include Tashi Relay.
    /// </summary>
    public sealed class PeerToPeerPlatform : Platform
    {
        public PeerToPeerPlatform(PlatformConfig config) :
            base(
                config,
                IPAddress.IsLoopback(config.BindAddress.Address) ? NetworkMode.Loopback : NetworkMode.Local
            )
        {
        }
    }

    /// <summary>
    /// Enables use of Tashi Relay, which helps peers to form direct connections
    /// through NAT hole punching and acts as a proxy for peers that are unable
    /// to make direct connections.
    /// </summary>
    public sealed class TashiRelayPlatform : Platform
    {
        public delegate void RelaySessionCreationHandler(RelayAllocation relayAllocation);

        public delegate void RelaySessionFailureHandler(Exception e);

        public TashiRelayPlatform(PlatformConfig config) :
            base(config, NetworkMode.Local)
        {
        }

        private SessionResultHandler? _sessionResultDelegate;

        /// <summary>
        /// This is hidden because the platform should only be started through
        /// `StartRelaySession` or `JoinRelaySession`.
        /// </summary>
        /// <param name="addressBookEntries"></param>
        /// <param name="joiningRunningSession"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private new static void Start(
            IEnumerable<AddressBookEntry>? addressBookEntries,
            bool joiningRunningSession,
            Guid? relaySessionId)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Asynchronously create a Tashi Relay session with the given API key
        /// and automatically join the session on success.
        ///
        /// On success, the first delegate will be invoked with the Relay
        /// server's address book entry. You should share this with the other
        /// participants.
        ///
        /// On error, the second delegate will be invoked.
        /// </summary>
        /// <param name="relayBaseUrl"></param>
        /// <param name="relayApiKey"></param>
        /// <param name="addressBook"></param>
        /// <param name="onSuccess">
        /// The address book containing entries for all peers allowed to
        /// participate in the session.
        /// </param>
        /// <param name="onError"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void StartRelaySession(string relayBaseUrl, string relayApiKey,
            IEnumerable<AddressBookEntry> addressBook,
            RelaySessionCreationHandler onSuccess,
            RelaySessionFailureHandler onError)
        {
            if (relayApiKey.Length == 0)
            {
                throw new ArgumentException("The Tashi Relay API key hasn't been set");
            }

            if (_sessionResultDelegate != null)
            {
                throw new InvalidOperationException("A Tashi Relay session allocation is already in progress");
            }

            if (_started)
            {
                throw new InvalidOperationException("The platform has already been started");
            }

            if (SynchronizationContext.Current == null)
            {
                // This is reachable in NUnit's tests when no Apartment is set.
                onError(new InvalidOperationException("There is no current synchronization context"));
                return;
            }

            var syncContext = SynchronizationContext.Current;

            // We need to retain this instance until the call completes or the platform instance is destroyed
            // so it doesn't get garbage collected while the async call is still running.
            //
            // If we get random segfaults after calling this method, then this is likely a faulty approach.
            _sessionResultDelegate =
                (result, relayPublicKeyDer, relayPublicKeyDerLen, relaySockAddr, relaySockAddrLen, relaySessionIdBytes,
                    relaySessionIdBytesLen) =>
                {
                    try
                    {
                        result.SuccessOrThrow("tce_relay_create_session");

                        var publicKey = PublicKey.FromPtr(relayPublicKeyDer, relayPublicKeyDerLen);
                        var sockAddr = SockAddr.FromPtr(relaySockAddr, relaySockAddrLen);
                        var entry = new DirectAddressBookEntry(sockAddr.IPEndPoint, publicKey, true);

                        Guid relaySessionId;
                        unsafe
                        {
                            relaySessionId = GuidExtensions.FromBigEndianBytes(
                                new Span<byte>(relaySessionIdBytes.ToPointer(), (int)relaySessionIdBytesLen)
                            );
                        }

                        AddAddressBookEntry(entry);

                        base.Start(addressBook, false, relaySessionId);

                        syncContext.Post(_ => onSuccess(new RelayAllocation(entry, relaySessionId)), null);
                    }
                    catch (Exception e)
                    {
                        syncContext.Post(_ => onError(e), null);
                    }
                    finally
                    {
                        // Allow the call to be retried if it failed.
                        _sessionResultDelegate = null;
                    }
                };

            SetAddressBook(addressBook);

            try
            {
                tce_relay_create_session(handle, relayBaseUrl, relayApiKey, _sessionResultDelegate)
                    .SuccessOrThrow("tce_relay_create_session");
            }
            catch (Exception)
            {
                _sessionResultDelegate = null;
                throw;
            }
        }

        /// <summary>
        /// Joins a newly create relay session.
        /// </summary>
        /// <param name="addressBook">This is the original address book without the relay address.</param>
        /// <param name="relayAllocation"></param>
        public void JoinRelaySession(
            IEnumerable<AddressBookEntry> addressBook,
            RelayAllocation relayAllocation
        )
        {
            SetAddressBook(addressBook);
            AddAddressBookEntry(relayAllocation.Address);
            base.Start(null, false, relayAllocation.SessionId);
        }

        [DllImport("tce_ffi", EntryPoint = "tce_relay_create_session",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_relay_create_session(
            IntPtr platform,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? baseUrl,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? apiKey,
            SessionResultHandler onSessionResult
        );

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SessionResultHandler(
            Result result,
            IntPtr relayPublicKeyDer,
            UInt32 relayPublicKeyDerLen,
            IntPtr relaySockAddr,
            UInt32 relaySockAddrLen,
            IntPtr relaySessionId,
            UInt32 relaySessionIdLen
        );
    }

    /// <summary>
    /// This kind of platform enables you to handle the messaging between peers.
    /// The platform will handle everything else, such as the fair ordering of
    /// events and managing when peers can join or leave an existing session.
    /// </summary>
    public sealed class ExternalTransmitPlatform : Platform
    {
        public ExternalTransmitPlatform(PlatformConfig config) :
            base(config, NetworkMode.External)
        {
        }

        public struct ExternalReceiveBuffer
        {
            public IntPtr pointer;
            public UInt64 length;
        }

        public ExternalReceiveBuffer ExternalReceivePrepare(SockAddr addr)
        {
            tce_external_recv_prepare(handle, MaxRelayDataLen, out var buf, out var bufLen)
                .SuccessOrThrow("tce_external_recv_prepare");

            return new ExternalReceiveBuffer()
            {
                pointer = buf,
                length = bufLen
            };
        }

        public void ExternalReceiveCommit(UInt64 writtenLength, SockAddr address)
        {
            tce_external_recv_commit(handle, writtenLength, ref address, (UInt32)address.Len)
                .SuccessOrThrow("tce_external_recv_commit");
        }

        public ExternalTransmit GetExternalTransmit()
        {
            var result = tce_external_transmit_get(handle, out var transmit);

            if (result != Result.Success && result != Result.TransmitQueueEmpty)
                throw new ResultException(result, "tce_external_transmit_get");

            return new ExternalTransmit(transmit);
        }

        [DllImport("tce_ffi", EntryPoint = "tce_external_recv_prepare",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_external_recv_prepare(
            IntPtr platform,
            UInt64 bufCapacity,
            out IntPtr buf,
            out UInt64 len
        );

        [DllImport("tce_ffi", EntryPoint = "tce_external_recv_commit",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_external_recv_commit(
            IntPtr platform,
            UInt64 writtenLen,
            ref SockAddr sockAddr,
            // `socklen_t` is defined to be 32 bits
            UInt32 sockAddrLen
        );

        [DllImport("tce_ffi", EntryPoint = "tce_external_transmit_get",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_external_transmit_get(
            IntPtr platform,
            out IntPtr transmitOut
        );
    }
}
