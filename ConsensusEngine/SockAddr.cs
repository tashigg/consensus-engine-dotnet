using System;
using System.Buffers.Binary;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ConsensusEngine.Tests")]
namespace Tashi.ConsensusEngine
{
    /// <summary>
    /// A Socket Address.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 128)]
    public struct SockAddr : IEquatable<SockAddr>
    {
        // Total struct size is 128 bytes, minus 2 bytes for AddressFamily
        private const int DataLen = 126;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        private byte[] _addressFamily;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = DataLen)]
        private byte[] _data;

        // C#'s AddressFamily doesn't match up to the native `AF_*` values and
        // it's 4 bytes instead of 2.
        /// <summary>
        /// The address family of this socket address.
        /// </summary>
        public readonly AddressFamily AddressFamily => NativeAddressFamily.ToAddressFamily(_addressFamily);

        internal readonly UInt64 Len => 128;

        /// <summary>
        /// The endpoint for this socket address.
        /// </summary>
        public IPEndPoint IPEndPoint
        {
            readonly get
            {
                UInt16 port = BinaryPrimitives.ReadUInt16BigEndian(PortSpan);
                IPAddress address = AddressFamily switch
                {
                    AddressFamily.InterNetwork => new IPAddress(_data[2..6]),
                    // sockaddr_in6 actually has the `flowinfo` field before the address, which is 4 bytes.
                    // Then the scope identifier follows the address, which for most intents and purposes is zero.
                    AddressFamily.InterNetworkV6 => new IPAddress(_data[6..22], BinaryPrimitives.ReadUInt16BigEndian(_data[22..])),
                    _ => throw new InvalidOperationException($"SockAddr.AddressFamily is not an IP subtype: {AddressFamily}"),
                };
                return new IPEndPoint(address, port);
            }

            set
            {
                _addressFamily = NativeAddressFamily.FromAddressFamily(value.AddressFamily);

                BinaryPrimitives.WriteUInt16BigEndian(PortSpan, (ushort)value.Port);

                value.Address.TryWriteBytes(AddrSpan, out _);
            }
        }

        public bool HasClientId
        {
            get
            {
                if (NativeAddressFamily.ToAddressFamily(_addressFamily) != AddressFamily.InterNetworkV6) return false;

                return
                    BinaryPrimitives.ReadUInt16BigEndian(PortSpan) == ClientIdPort &&
                    AddrSpan.StartsWith(new ReadOnlySpan<byte>(ClientIdAddressPrefix));
            }
        }

        public ulong? ClientId
        {
            get
            {
                // This checks that the address is IPv6 and that it has our designated prefix,
                // then reads the 64-bit address portion as the client ID.
                if (!HasClientId) return null;
                return BinaryPrimitives.ReadUInt64BigEndian(AddrSpan[ClientIdAddressPrefix.Length..]);
            }
        }


        private readonly Span<byte> AddrSpan => AddressFamily switch
        {
            AddressFamily.InterNetwork => new Span<byte>(_data, 2, 4),
            // sockaddr_in6 actually has the `flowinfo` field before the address, which is 4 bytes.
            // For our intents and purposes that is always zero.
            AddressFamily.InterNetworkV6 => new Span<byte>(_data, 6, 16),
            _ => throw new InvalidOperationException($"SockAddr.AddressFamily is not an IP subtype: {AddressFamily}")
        };

        // Unintuitively, the port is at the beginning of the data.
        private readonly Span<byte> PortSpan => new Span<byte>(_data, 0, 2);

        public static SockAddr FromClientId(ulong clientId)
        {
            SockAddr addrOut = new SockAddr
            {
                _addressFamily = NativeAddressFamily.FromAddressFamily(AddressFamily.InterNetworkV6),
                _data = new byte[DataLen]
            };

            // IMPORTANT: slicing a `Span` creates views into the underlying data,
            // but slicing a `byte[]` creates copies.
            var dataSpan = new Span<byte>(addrOut._data);

            var portSpan = dataSpan[..2];

            BinaryPrimitives.WriteUInt16BigEndian(portSpan, ClientIdPort);

            var addrPrefixSpan = new ReadOnlySpan<byte>(ClientIdAddressPrefix);

            // By default we just initialize that to zero.
            var addrSpan = addrOut.AddrSpan;

            var successful = addrPrefixSpan.TryCopyTo(addrSpan);

            System.Diagnostics.Debug.Assert(successful);

            var clientIdSpan = addrSpan[ClientIdAddressPrefix.Length..];

            BinaryPrimitives.WriteUInt64BigEndian(clientIdSpan, clientId);

            return addrOut;
        }

        // `socklen_t` is defined to be 32 bits (signed on Windows, unsigned on *Nix but it doesn't really matter).
        internal static SockAddr FromPtr(IntPtr sockAddr, uint sockAddrLen)
        {
            // A `sockaddr` must always at least contain a 2-byte address family value,
            // and is defined to be 128 bytes or less.
            if (sockAddrLen < 2 || sockAddrLen > 128)
            {
                throw new ArgumentException($"sockAddrLen out of range: {sockAddrLen}");
            }

            var sockAddrOut = new SockAddr
            {
                _data = new byte[126],
                _addressFamily = new byte[2]
            };

            // macOS and some other BSDs use the first byte to represent `ss_len`,
            // but others use the address family field as 2 bytes. Some BSDs
            // even got rid of `ss_len`.
            Marshal.Copy(sockAddr, sockAddrOut._addressFamily, 0, 2);

            // Offset to the actual data.
            var sockAddrData = sockAddr + 2;

            Marshal.Copy(sockAddrData, sockAddrOut._data, 0, (int)sockAddrLen - 2);

            return sockAddrOut;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{IPEndPoint} (clientId = {ClientId})";
        }

        private static readonly byte[] ClientIdAddressPrefix =
        {
            // `fd00:/8` designates this as a locally assigned ULA (unique local address).
            // `fc00::/8` is reserved for future use.
            0xfd,
            // The next 5 bytes are the global ID.
            // Meant to be random, but this lets us unambiguously identify generated addresses
            // when we support mixed networking topology.
            //
            // It's unlikely for another organization to randomly choose this global ID
            // *and* want to use TNT in their network.
            (byte)'T',
            (byte)'a',
            (byte)'s',
            (byte)'h',
            (byte)'i',
            // Subnet ID (2 bytes), just choosing zero for this one.
            0,
            0,
        };

        private const ushort ClientIdPort = 0x6767; // 'gg'

        public readonly bool Equals(SockAddr other)
        {
            return _addressFamily.SequenceEqual(other._addressFamily) && _data.SequenceEqual(other._data);
        }

        public override readonly int GetHashCode()
        {
            var hasher = new HashCode();

            foreach (var b in _addressFamily)
            {
                hasher.Add(b);
            }

            // `hashCode.AddBytes` is unavailable
            foreach (var b in _data)
            {
                hasher.Add(b);
            }

            return hasher.ToHashCode();
        }
    }

    /// <summary>
    /// A supplement for the standard `AddressFamily` enum with correct constants for the current platform.
    /// </summary>
    internal class NativeAddressFamily
    {
        /// <summary>
        /// Corresponds to AF_INET
        /// </summary>
        private const UInt16 InterNetwork = 2;

        /// <summary>
        /// Corresponds to AF_INET6
        /// </summary>
        private static UInt16 InterNetworkV6
        {
            get
            {
                // Annoyingly, `AF_INET6` has a different value on *every* platform.
                // While the major modern operating systems all started off with copies of the Berkeley Sockets API,
                // each of them implemented IPv6 support separately, and apparently never bothered
                // to agree on a single constant value.

                // We can't use https://learn.microsoft.com/en-us/dotnet/api/system.platformid?view=netstandard-2.1
                // because the comment on the `MacOSX` constant says it's not returned on .NET Core and it instead
                // returns `Unix`, which is absolutely useless.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return 23;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return 30;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return 10;
                }

                throw new Exception("unsupported operating system");
            }
        }

        public static AddressFamily ToAddressFamily(byte[] nativeAddressFamilyBytes)
        {
            System.Diagnostics.Debug.Assert(nativeAddressFamilyBytes.Length == 2);

            var value = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? nativeAddressFamilyBytes[1] : BitConverter.ToUInt16(nativeAddressFamilyBytes);

            // Can't use `switch()` here because `InterNetwork6` isn't a constant.
            if (value == InterNetwork)
            {
                return AddressFamily.InterNetwork;
            }

            if (value == InterNetworkV6)
            {
                return AddressFamily.InterNetworkV6;
            }

            throw new ArgumentException($"unknown AddressFamily value: {value}");
        }

        public static byte[] FromAddressFamily(AddressFamily addressFamily)
        {
            var nativeAddressFamily = addressFamily switch
            {
                AddressFamily.InterNetwork => InterNetwork,
                AddressFamily.InterNetworkV6 => InterNetworkV6,
                _ => throw new ArgumentException($"unsupported AddressFamily type: {addressFamily}")
            };

            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? new byte[] { 0, (byte)nativeAddressFamily } : BitConverter.GetBytes(nativeAddressFamily);
        }
    }
}
