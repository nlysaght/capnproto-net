#if UNSAFE
#define UNMANAGED
#endif

using CapnProto.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapnProto
{
    
    /// <summary>
    /// Need a separate builder/factory for creating a buffer segment factory. 
    /// NOTE:: In reality see these being created from an IOC like TinyIOC....
    /// </summary>
    /// <remarks>
    /// Wanted to remove the inbuilt dependency on the static Cache(T) class as it's not static anymore, also makes it easier to test.
    /// Wanted to remove the responsibility of managing the caching behavior from the BufferSegmentFactory and have it placed a pipeline 
    /// behavior. Means during disposal we can put in additional behaviors for things like Caching, again for testing reasons and also for single responsibility principals.
    /// We can now inject the GC wrapper for testing.
    /// </remarks>
    class BufferedStreamSegmentFactoryBuilder
    {
        private readonly IGCWrapper gc;
        private ScopedCache<BufferedStreamSegmentFactory> cache;
        public BufferedStreamSegmentFactoryBuilder(IGCWrapper gc)
        {
            this.gc = gc;
            cache = new ScopedCache<BufferedStreamSegmentFactory>(gc); 
        }
        public BufferedStreamSegmentFactory Create(Stream source, long length, bool leaveOpen)
        {
            var obj = cache.Pop() ?? new BufferedStreamSegmentFactory(DisposeAction, source, length, leaveOpen);
            obj.Init(source, length, leaveOpen);
            return obj;
        }
        /// <summary>
        /// Manage caching internally in this class for the moment. Keeps the BufferedStreamSegmentFactory clean and testable.
        /// </summary>
        /// <param name="instance"></param>
        private void DisposeAction(BufferedStreamSegmentFactory instance)
        {
            cache.Push(instance);
        }
    }

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
        public byte[] Buffer {get; private set;}
    }

    public class SingleInstanceOnlyException<T> : Exception
    {
        public SingleInstanceOnlyException()
            : base(string.Format("You are only allowed a single instance of this class {0}", typeof(T).FullName))
        {
        }
    }

    /// <summary>
    /// Demand that only a single instance of a class exists.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SingletonInstance<T> where T: class
    {
        private static SingletonInstance<T> singleInstance = null;
        private static readonly object syncRoot = new Object();
        public SingletonInstance()
        {
            if (singleInstance == null)
            {
                lock (syncRoot)
                {
                    if (singleInstance != null)
                    {
                        throw new SingleInstanceOnlyException<UnManagedBufferProvider>();
                    }
                    singleInstance = this;
                }
            }
            else
            {
                throw new SingleInstanceOnlyException<UnManagedBufferProvider>();
            }
        }
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
    internal class UnManagedBufferProvider 
        : SingletonInstance<UnManagedBufferProvider>, IUnmanagedBufferProvider
    {
        private byte[] sharedBuffer;

        public UnManagedBufferProvider(int messageWordLength)
        {
            Buffer = sharedBuffer;
        }
        public byte[] Buffer { get; private set; }
        public byte[] PopBuffer()
        {
            return Interlocked.Exchange(ref sharedBuffer, null) ?? new byte[1024 * Message.WordLength];
        }
        public void PushBuffer(byte[] buffer)
        {
            if (buffer != null) Interlocked.Exchange(ref sharedBuffer, buffer);
        }
    }

    class BufferedStreamSegmentFactory : SegmentFactory
    {
        Stream source;
        long lastWord, remainingWords;
        bool leaveOpen;
        readonly Action<BufferedStreamSegmentFactory> disposeAction;
//Depending on implementation required define the buffer provider.
#if UNMANAGED
        IUnmanagedBufferProvider bufferProvider;
#else
        IBufferProvider bufferProvider;
#endif

        public BufferedStreamSegmentFactory(Action<BufferedStreamSegmentFactory> disposeAction, Stream source, long length, bool leaveOpen)
        {
            this.disposeAction = disposeAction;
            Init(source, length, leaveOpen);
        }
        public override void Dispose()
        {
            //TODO: Clarify with Marc if his intent here was to call the base.Dispose() I put this in, Was missing from initial implementation.
            base.Dispose();
            disposeAction(this);
        }
        protected override void Reset(bool recyclying)
        {
            if(source != null && !leaveOpen)
            {
                try { source.Dispose(); } catch { }
            }
            source = null;
            lastWord = remainingWords = 0;
            leaveOpen = false;
            base.Reset(recyclying);
        }
        internal void Init(Stream source, long length, bool leaveOpen)
        {
            this.source = source;
            this.lastWord = 0;
            this.leaveOpen = leaveOpen;
            if (length < 0) remainingWords = -1;
            else remainingWords = length >> 3;
        }
        private void CheckLastWord(long wordOffset, int delta)
        {
            if (this.lastWord != wordOffset)
                throw new InvalidOperationException();
            this.lastWord += delta;
        }
        protected override bool TryReadWord(long wordOffset, out ulong value)
        {
            CheckLastWord(wordOffset, 1);
            if (this.remainingWords == 0)
            {
                value = 0;
                return false;
            }
            byte[] scratch = new byte[8];
            if(!Read(source, scratch, 8))
            {
                value = 0;
                return false;
            }
            
            if(this.remainingWords > 0) this.remainingWords--;
            value = BitConverter.ToUInt64(scratch, 0);
            return true;
        }
        bool Read(Stream source, byte[] buffer, int count)
        {
            int offset = 0, read;
            while (count > 0 && (read = source.Read(buffer, offset, count)) > 0)
            {
                offset += read;
                count -= read;
            }
            return count == 0;
        }

        protected override ISegment CreateEmptySegment()
        {
#if UNMANAGED
            return PointerSegment.Create(true);
#else
            return BufferSegment.Create();
#endif
        }

        protected override bool InitializeSegment(ISegment segment, long wordOffset, int totalWords, int activeWords)
        {
            CheckLastWord(wordOffset, totalWords);
            
            if(this.remainingWords >= 0 && this.remainingWords < totalWords)
            {
                return false;
            }

#if UNMANAGED
            bufferProvider = new UnManagedBufferProvider(Message.WordLength);
            var ptr = default(IntPtr);
            byte[] buffer = null;
            try {
                long bytes = ((long)totalWords) << 3;
                ptr = Marshal.AllocHGlobal(totalWords << 3);
                buffer = bufferProvider.PopBuffer();

                IntPtr writeHead = ptr;
                while(bytes > 0)
                {
                    int read = (int)Math.Min(bytes, buffer.Length);
                    if (!Read(source, buffer, read)) throw new EndOfStreamException();
                    Marshal.Copy(buffer, 0, writeHead, read);
                    writeHead += read;
                    bytes -= read;
                }
                ((PointerSegment)segment).Initialize(ptr, totalWords, activeWords);
                ptr = default(IntPtr);
            }
            finally
            {
                bufferProvider.PushBuffer(buffer);
                if (ptr != default(IntPtr)) Marshal.FreeHGlobal(ptr);
            }
#else
            bufferProvider = new ManagedBufferProvider(totalWords << 3);
            if (!Read(source, bufferProvider.Buffer, bufferProvider.Buffer.Length))
            {
                return false;
            }
            ((BufferSegment)segment).Init(bufferProvider.Buffer, 0, totalWords, activeWords);
#endif
            if (this.remainingWords > 0) this.remainingWords -= totalWords;
            return true;
        }
    }
}
