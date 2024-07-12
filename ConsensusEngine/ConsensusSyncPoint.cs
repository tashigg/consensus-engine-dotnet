// This copies data from the native representations. We could use more unsafe
// or existing third-party unmanaged collections to avoid this if we need to.
// Something like https://github.com/ikorin24/UnmanagedArray

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Tashi.ConsensusEngine
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeConsensusSyncPoint
    {
        internal readonly UInt64 new_epoch_index;
        internal readonly IntPtr joining_creators;
        internal readonly UInt32 joining_creators_len;

        internal readonly IntPtr fallen_behind_creators;
        internal readonly UInt32 fallen_behind_creators_len;
        internal readonly IntPtr leaving_creators;
        internal readonly UInt32 leaving_creators_len;
        internal readonly IntPtr kicked_for_cheating;
        internal readonly UInt32 kicked_for_cheating_len;
        internal readonly IntPtr safe_to_forget_creators;
        internal readonly UInt32 safe_to_forget_creators_len;

        internal readonly IntPtr just_synced_addresses;
        internal readonly UInt32 just_synced_addresses_len;

        internal readonly bool just_synced;
        internal readonly bool session_ended;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeCreatorId
    {
        internal readonly IntPtr id;
        internal readonly UInt32 id_len;
    }

    /// <summary>
    ///  The type of node.
    /// </summary>
    public enum NodeType : byte
    {
        /// <summary>
        /// A node that participates in consensus.
        /// </summary>
        Normal,
        /// <summary>
        /// A node used for relaying information between normal nodes.
        /// </summary>
        Relay
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeNewCreatorAddress
    {
        internal readonly NativeCreatorId creator;
        internal readonly NodeType node_type;
        internal readonly IntPtr endpoint;
    }

    /// <summary>
    /// Information required for adding a new creator.
    /// </summary>
    public struct NewCreatorAddress
    {
        /// <summary>
        /// The creator's ID.
        /// </summary>
        public PublicKey CreatorId { get; }
        /// <summary>
        /// The type of node the creator will be.
        /// </summary>
        public NodeType NodeType { get; }

        /// <summary>
        /// The self-declared address of the creator.
        /// </summary>
        public string Endpoint { get; }

        internal NewCreatorAddress(NativeNewCreatorAddress native)
        {
            CreatorId = PublicKey.FromPtr(native.creator.id, native.creator.id_len);
            NodeType = native.node_type;
            Endpoint = Marshal.PtrToStringUTF8(native.endpoint);
        }
    }

    /// <summary>
    /// A sync point in consensus. These happen ocassionally over the duration of the session.
    /// </summary>
    public readonly struct ConsensusSyncPoint
    {
        /// <summary>
        /// The epoch as-of this sync point.
        /// </summary>
        public UInt64 NewEpochIndex { get; }

        /// <summary>
        /// The creators that have joined since the last sync point.
        /// </summary>
        public List<NewCreatorAddress> JoiningCreators { get; }

        /// <summary>
        /// The creators that fell behind in the last sync point.
        /// </summary>
        public List<PublicKey> FallenBehindCreators { get; }

        /// <summary>
        /// The creators that are in the process of leaving as of the last sync point.
        /// </summary>
        public List<PublicKey> LeavingCreators { get; }

        /// <summary>
        /// Creators that were kicked for cheating since the last sync point.
        /// "Cheating" in this context is specifically trying to cheat the consensus algorithm by submitting conflicting events to different peers,
        /// which is detected by the consensus algorithm and an automatic vote to kick the cheating creator is initiated.
        /// </summary>
        public List<PublicKey> KickedForCheating { get; }

        /// <summary>
        /// The creators that are now completely gone. They have no effect on consensus anymore, and they're no longer part of the session.
        /// </summary>
        public List<PublicKey> SafeToForgetCreators { get; }

        /// <summary>
        /// If we have just synced, we'll receive a sync point with JustSynced set to true.
        /// In that case, JoiningCreators, FallenBehindCreators, LeavingCreators,
        /// and SafeToForgetCreators will all be empty, and JustSyncedAddresses
        /// will contain the address book.
        /// </summary>
        public List<NewCreatorAddress> JustSyncedAddresses { get; }

        /// <summary>
        /// True if we have just synced with the session.  This happens when we join the session, 
        /// when we re-sync with the session after having fallen behind, and when we reset the message stream.
        /// </summary>
        public bool JustSynced { get; }

        /// <summary>
        /// If a vote to end session passes in epoch N, then the last message from the session will be the
        /// sync point between epochs N and N + 1 with SessionEnded set to true.
        /// </summary>
        public bool SessionEnded { get; }

        /// <summary>
        /// </summary>
        /// <param name="native">You can free the native value as soon as the constructor has completed.</param>
        internal ConsensusSyncPoint(NativeConsensusSyncPoint native)
        {
            NewEpochIndex = native.new_epoch_index;

            JoiningCreators = CopyNativeArray(native.joining_creators, (int)native.joining_creators_len,
                (NativeNewCreatorAddress addr) => new NewCreatorAddress(addr));

            FallenBehindCreators = CopyNativeArray<NativeCreatorId, PublicKey>(native.fallen_behind_creators,
                (int)native.fallen_behind_creators_len,
                PublicKey.FromNativeCreatorId);

            LeavingCreators = CopyNativeArray<NativeCreatorId, PublicKey>(native.leaving_creators,
                (int)native.leaving_creators_len,
                PublicKey.FromNativeCreatorId);

            KickedForCheating = CopyNativeArray<NativeCreatorId, PublicKey>(native.kicked_for_cheating,
                (int)native.kicked_for_cheating_len,
                PublicKey.FromNativeCreatorId);

            SafeToForgetCreators = CopyNativeArray<NativeCreatorId, PublicKey>(native.safe_to_forget_creators,
                (int)native.safe_to_forget_creators_len,
                PublicKey.FromNativeCreatorId);

            JustSyncedAddresses = CopyNativeArray(native.just_synced_addresses, (int)native.just_synced_addresses_len, 
                (NativeNewCreatorAddress addr) => new NewCreatorAddress(addr));

            JustSynced = native.just_synced;

            SessionEnded = native.session_ended;
        }

        private static List<T> CopyNativeArray<TNative, T>(IntPtr ptr, int length, Func<TNative, T> constructor)
        {
            var list = new List<T>(length);

            for (var i = 0; i < length; ++i)
            {
                var address = IntPtr.Add(ptr, i * Marshal.SizeOf<TNative>());
                var nativeValue = Marshal.PtrToStructure<TNative>(address);
                list.Add(constructor(nativeValue));
            }

            return list;
        }

        public override readonly string ToString()
        {
            return $"ConsensusSyncPoint: NewEpochIndex={NewEpochIndex}, JoiningCreators.Count={JoiningCreators.Count}, FallenBehindCreators.Count={FallenBehindCreators.Count}, LeavingCreators.Count={LeavingCreators.Count}, SafeToForgetCreators.Count={SafeToForgetCreators.Count}";
        }
    }
}
