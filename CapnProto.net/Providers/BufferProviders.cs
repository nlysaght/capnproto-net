using CapnProto.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapnProto.Providers
{
    /// <summary>
    /// Define a buffer accessor which is required for managed and unmanaged access in the BufferedStreamSegmentFactory.
    /// </summary>
    public interface IBufferProvider
    {
        byte[] Buffer { get; }
    }
    /// <summary>
    /// The unmanaged provider also needs additional functionality for its behavior.
    /// </summary>
    public interface IUnmanagedBufferProvider : IBufferProvider
    {
        byte[] PopBuffer();
        void PushBuffer(byte[] buffer);
    }

    /// <summary>
    /// Implementation of the managed buffer provider 
    /// </summary>
    internal class ManagedBufferProvider : IBufferProvider
    {
        public ManagedBufferProvider(int bufferSize)
        {
            Buffer = new byte[bufferSize];
        }
        public byte[] Buffer { get; private set; }
    }



    /// <summary>
    /// Moved the implementation of the Unmanaged static buffer to this implementation.
    /// </summary>
    /// <remarks>
    /// Remove some convoluted code from the BufferedStreamSegmentFactory and allows us to easily change the
    /// buffering mechanism.
    /// CLARIFY:: Basically this class as a singleton would remove the static decoration from it
    /// and everything would work as it currently does in static mode.
    /// </remarks>
    internal class UnmanagedBufferProvider
        : SingletonInstance<UnmanagedBufferProvider>, IUnmanagedBufferProvider
    {
        private byte[] sharedBuffer;
        private readonly int messageWordLength;

        public UnmanagedBufferProvider(int messageWordLength)
        {
            if (messageWordLength <= 0 || messageWordLength >= 16)
            {
                throw new ArgumentOutOfRangeException("messageWordLength");
            }
            this.messageWordLength = messageWordLength;
        }

        byte[] IUnmanagedBufferProvider.PopBuffer()
        {
            return Interlocked.Exchange(ref sharedBuffer, null) ?? new byte[1024 * messageWordLength];
        }

        void IUnmanagedBufferProvider.PushBuffer(byte[] buffer)
        {
            if (buffer != null) Interlocked.Exchange(ref sharedBuffer, buffer);
        }

        byte[] IBufferProvider.Buffer
        {
            get { return sharedBuffer; }
        }
    }
}
