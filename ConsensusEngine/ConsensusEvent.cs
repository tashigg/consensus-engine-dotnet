using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tashi.ConsensusEngine
{
    // Ideally this would be a tagged union or higher level equivalent, but the
    // way to achieve this is horrific without a third party library (explicit
    // layout). Using explicit layout also causes our tests to fail to run
    // without any indication that it's the cause.
    /// <summary>
    /// A union of either a <see cref="T:ConsensusEvent"/> or <see cref="T:ConsensusSyncPoint"/>. Only one of these members will exist.
    /// </summary>
    public readonly struct Event
    {
        public ConsensusEvent? ConsensusEvent { get; }
        public ConsensusSyncPoint? ConsensusSyncPoint { get; }

        internal Event(ConsensusEvent consensusEvent)
        {
            ConsensusSyncPoint = null;
            ConsensusEvent = consensusEvent;
        }

        internal Event(ConsensusSyncPoint consensusSyncPoint)
        {
            ConsensusEvent = null;
            ConsensusSyncPoint = consensusSyncPoint;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeConsensusEvent
    {
        // This is an array of variable length arrays. Each transaction has a
        // 4 byte length prefix.
        internal readonly IntPtr packed_transactions;
        internal readonly UInt32 packed_transactions_len;
        internal readonly UInt64 timestamp_created;
        internal readonly UInt64 timestamp_received;
        internal readonly IntPtr creator_id;
        internal readonly UInt32 creator_id_len;
        internal readonly IntPtr rng_seed;
        internal readonly UInt32 rng_seed_len;
    }

    /// <summary>
    /// An event which has reached consensus.
    /// </summary>
    public class ConsensusEvent : IDisposable
    {

        /// <summary>
        /// The timestamp this event was originally created in unix-timestamp nanoseconds, according to its creator.
        /// </summary>
        public ulong TimestampCreated => _nativeConsensusEvent.timestamp_created;

        /// <summary>
        /// The timestamp this event reached consensus in unix-timestamp nanoseconds.
        /// This is agreed upon by the session.
        /// </summary>
        public ulong TimestampReceived => _nativeConsensusEvent.timestamp_received;

        /// <summary>
        /// The public key of this event's creator.
        /// </summary>
        public PublicKey CreatorPublicKey { get; }

        /// <summary>
        /// Consensus RNG seeded via this event.
        /// </summary>
        /// <seealso cref="T:ConsensusRng"/>
        public ConsensusRng ConsensusRng
        {
            get
            {
                if (_nativeConsensusEvent.rng_seed_len > int.MaxValue)
                {
                    throw new Exception("the RNG seed length doesn't fit in an int");
                }

                unsafe
                {
                    return new ConsensusRng(
                        new ReadOnlySpan<byte>(
                            (void*)_nativeConsensusEvent.rng_seed,
                            (int)_nativeConsensusEvent.rng_seed_len)
                    );
                }
            }
        }

        private readonly IntPtr _eventPointer;
        private readonly NativeConsensusEvent _nativeConsensusEvent;

        internal ConsensusEvent(IntPtr eventPointer)
        {
            _eventPointer = eventPointer;
            _nativeConsensusEvent = Marshal.PtrToStructure<NativeConsensusEvent>(eventPointer);

            if (_nativeConsensusEvent.packed_transactions_len > Int32.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(_nativeConsensusEvent.packed_transactions_len));
            }

            if (_nativeConsensusEvent.creator_id_len == 0)
            {
                throw new Exception("The event doesn't have a creator");
            }

            CreatorPublicKey = PublicKey.FromPtr(_nativeConsensusEvent.creator_id, _nativeConsensusEvent.creator_id_len);

            Debug.Assert(_nativeConsensusEvent.rng_seed != IntPtr.Zero,
            "BUG: event reported before consensus");

            _transactionCount = 0;
        }

        /// <summary>
        /// Used to handle the processing of an individual transaction which
        /// is stored in unmanaged memory.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the iteration should continue.
        /// </returns>
        /// <see cref="ForEachTransaction"/>
        public delegate bool TransactionHandler(ReadOnlySpan<byte> handler);

        private int _transactionCount;

        /// <summary>
        /// Gets the total number of transactions.
        /// </summary>
        public int TransactionCount
        {
            get
            {
                if (_transactionCount != 0)
                {
                    return _transactionCount;
                }

                var newTransactionCount = 0;

                ForEachTransaction(t =>
                {
                    newTransactionCount++;
                    return true;
                });

                _transactionCount = newTransactionCount;

                return _transactionCount;
            }
        }

        /// <summary>
        /// Process each transaction.
        ///
        /// Transactions live in unmanaged memory. `IEnumerable` isn't
        /// implemented as it prevents us from minimizing heap allocations.
        /// </summary>
        /// <param name="handler"></param>
        public void ForEachTransaction(TransactionHandler handler)
        {
            ReadOnlySpan<byte> packedTransactions;

            unsafe
            {
                packedTransactions = new ReadOnlySpan<byte>((void*)_nativeConsensusEvent.packed_transactions,
                    // The constructor verifies that this length fits into int.
                    (int)_nativeConsensusEvent.packed_transactions_len);
            }

            for (var offset = 0; offset < _nativeConsensusEvent.packed_transactions_len;)
            {
                var length = MemoryMarshal.Read<Int32>(packedTransactions.Slice(offset, 4));
                offset += 4;

                var transaction = packedTransactions.Slice(offset, length);
                offset += length;
                if (!handler(transaction))
                {
                    return;
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return
                $"ConsensusEvent: TimestampCreated={TimestampCreated}, TimestampReceived={TimestampReceived}, CreatorPublicKey={CreatorPublicKey}, TransactionCount={TransactionCount}";
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            tce_event_free(_eventPointer);
        }

        [DllImport("tce_ffi", EntryPoint = "tce_event_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void tce_event_free(IntPtr consensusEvent);
    }
}
