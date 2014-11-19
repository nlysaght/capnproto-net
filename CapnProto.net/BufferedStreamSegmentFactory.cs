#if UNSAFE
#define UNMANAGED
#endif

using CapnProto.Base;
using CapnProto.Providers;
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
    internal interface IBufferedStreamSegmentFactoryBuilder
    {
        BufferedStreamSegmentFactory Create(Stream source, long length, bool leaveOpen);
    }
    class BufferedStreamSegmentFactoryBuilder 
        : 
#if PRODUCTION
 SingletonInstance<BufferedStreamSegmentFactoryBuilder>,  
#endif
        IBufferedStreamSegmentFactoryBuilder
    {
        private ScopedCache<BufferedStreamSegmentFactory> cache;
        public BufferedStreamSegmentFactoryBuilder(IGCWrapper gc)
        {
            cache = new ScopedCache<BufferedStreamSegmentFactory>(gc); 
        }
        /// <summary>
        /// Manage caching internally in this class for the moment. Keeps the BufferedStreamSegmentFactory clean and testable.
        /// </summary>
        /// <param name="instance"></param>
        private void DisposeAction(BufferedStreamSegmentFactory instance)
        {
            cache.Push(instance);
        }

        BufferedStreamSegmentFactory IBufferedStreamSegmentFactoryBuilder.Create(Stream source, long length, bool leaveOpen)
        {
            var obj = cache.Pop() ?? new BufferedStreamSegmentFactory(DisposeAction, source, length, leaveOpen);
            obj.Init(source, length, leaveOpen);
            return obj;
        }
    }

    /// <summary>
    /// Instance of this class should always be creating using the BufferedStreamSegmentFactoryBuilder
    /// </summary>
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
            if (disposeAction == null)
                throw new ArgumentNullException("disposeAction");
            if (source == null)
                throw new ArgumentNullException("source");
            // TODO:: Do we need length validation here. I suppose this is stream length validation which should never be < 0.
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
                try 
                { 
                    source.Dispose(); 
                } 
                catch 
                { 
                    //TODO:: Clarify do we really want a silent exception here?
                }
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
            bufferProvider = new UnmanagedBufferProvider(Message.WordLength);
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
