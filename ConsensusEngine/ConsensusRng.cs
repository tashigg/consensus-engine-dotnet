using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tashi.ConsensusEngine
{
    /// <summary>
    /// A pseudorandom number generator (PRNG), seeded by the consensus algorithm.
    /// 
    /// While generally high quality this should NOT be considered crpytographically secure.
    /// </summary>
    public class ConsensusRng : Random, IDisposable
    {
        private IntPtr _ptr;

        internal ConsensusRng(ReadOnlySpan<byte> rngSeed)
        {
            unsafe
            {
                fixed (byte* wSigPtr = rngSeed)
                {
                    tce_rng_init((IntPtr)wSigPtr, (UInt32)rngSeed.Length, out _ptr)
                        .SuccessOrThrow("tce_rng_init");
                }
            }
        }

        /// <inheritdoc/>
        public override void NextBytes(byte[] bytes)
        {
            Debug.Assert(_ptr != IntPtr.Zero);

            unsafe
            {
                fixed (byte* bPtr = bytes)
                {
                    tce_rng_fill_bytes(_ptr, (IntPtr)bPtr, (UInt32)bytes.Length)
                        .SuccessOrThrow("tce_rng_fill_bytes");
                }
            }
        }

        /// <inheritdoc/>
        public override void NextBytes(Span<byte> buffer)
        {
            Debug.Assert(_ptr != IntPtr.Zero);

            unsafe
            {
                fixed (byte* bPtr = buffer)
                {
                    tce_rng_fill_bytes(_ptr, (IntPtr)bPtr, (UInt32)buffer.Length)
                        .SuccessOrThrow("tce_rng_fill_bytes");
                }
            }
        }

        /// <inheritdoc/>
        public override int Next()
        {
            Debug.Assert(_ptr != IntPtr.Zero);

            tce_rng_next_u32(_ptr, out var ret)
                .SuccessOrThrow("tce_rng_next_u32");

            unchecked
            {
                return (int)ret;
            }
        }

        // This method exists on `Random` in .NET 6 but not .NET Standard 2.1
        public long NextInt64()
        {
            unchecked
            {
                return (long)NextUInt64();
            }
        }

        /// <inheritdoc/>
        public UInt64 NextUInt64()
        {
            Debug.Assert(_ptr != IntPtr.Zero);

            tce_rng_next_u64(_ptr, out var ret)
                .SuccessOrThrow("tce_rng_next_u64");
            return ret;
        }

        /// <inheritdoc/>
        public override double NextDouble()
        {

            Debug.Assert(_ptr != IntPtr.Zero);

            tce_rng_next_f64(_ptr, out var ret)
                .SuccessOrThrow("tce_rng_next_f64");

            return ret;
        }

        /// <inheritdoc/>
        protected override double Sample()
        {
            return NextDouble();
        }

        ~ConsensusRng()
        {
            Dispose();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {
                tce_rng_free(_ptr);
                _ptr = IntPtr.Zero;
            }
        }

        [DllImport("tce_ffi", EntryPoint = "tce_rng_init",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_rng_init(
            IntPtr rngSeed,
            UInt32 rngSeedLen,
            out IntPtr rngOut
        );

        [DllImport("tce_ffi", EntryPoint = "tce_rng_fill_bytes",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_rng_fill_bytes(
            IntPtr rng,
            IntPtr bytes,
            UInt32 bytesLen
        );

        [DllImport("tce_ffi", EntryPoint = "tce_rng_next_u32",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_rng_next_u32(
            IntPtr rng,
            out UInt32 u32
        );

        [DllImport("tce_ffi", EntryPoint = "tce_rng_next_u64",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_rng_next_u64(
            IntPtr rng,
            out UInt64 u64
        );

        [DllImport("tce_ffi", EntryPoint = "tce_rng_next_f64",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern Result tce_rng_next_f64(
            IntPtr rng,
            out double f64
        );

        [DllImport("tce_ffi", EntryPoint = "tce_rng_free",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void tce_rng_free(IntPtr rng);
    }
}
