using System;
using System.Runtime.InteropServices;

namespace Tashi.ConsensusEngine
{
    public class ExternalTransmit : IDisposable
    {
        private IntPtr _ptr;

        public readonly SockAddr Address;

        public readonly IntPtr Packet;
        public readonly int PacketLen;

        internal ExternalTransmit(IntPtr ptr)
        {
            _ptr = ptr;

            try
            {
                var result = tce_external_transmit_get_addr(ptr, out var addr);

                if (result != Result.Success)
                {
                    throw new Exception($"error from tce_external_transmit_get_addr: {result}");
                }

                Address = addr;

                result = tce_external_transmit_get_packet(ptr, out var packet, out var packetLen);

                if (result != Result.Success)
                {
                    throw new Exception($"error from tce_external_transmit_get_packet: {result}");
                }

                Packet = packet;

                checked
                {
                    PacketLen = (int)packetLen;
                }
            }
            catch (Exception)
            {
                tce_external_transmit_destroy(ptr);
                throw;
            }
        }

        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {

                var result = tce_external_transmit_destroy(_ptr);
                if (result != Result.Success)
                {
                    throw new Exception($"error from tce_external_transmit_destroy: {result}");
                }

                _ptr = IntPtr.Zero;
            }
        }

        [DllImport("tce_ffi", EntryPoint = "tce_external_transmit_get_addr", CallingConvention = CallingConvention.Cdecl)]
        static extern Result tce_external_transmit_get_addr(
            IntPtr transmit,
            out SockAddr addr
        );

        [DllImport("tce_ffi", EntryPoint = "tce_external_transmit_get_packet", CallingConvention = CallingConvention.Cdecl)]
        static extern Result tce_external_transmit_get_packet(
            IntPtr transmit,
            out IntPtr packetOut,
            out UInt64 packetLenOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_external_transmit_destroy", CallingConvention = CallingConvention.Cdecl)]
        static extern Result tce_external_transmit_destroy(IntPtr transmit);
    }
}
