using CapnProto.Wrappers;
using System;

namespace CapnProto
{
    public interface IRecyclable : IDisposable
    {
        void Reset(bool recycling);
    }

    internal interface ICache<T> where T: class, IRecyclable
    {
        T Pop();
        void Push(T obj);
    }

    internal class ScopedCache<T> : ICache<T> where T: class, IRecyclable
    {
        private T recycled;
        internal IGCWrapper GC { get; private set; }
        public ScopedCache(IGCWrapper gc)
        {
            if (gc == null)
                throw new ArgumentNullException("gc");
            this.GC = gc;
        }

        public T Pop()
        {
            var tmp = recycled;
            if (tmp != null)
            {
                recycled = null;
                GC.ReRegisterForFinalize(tmp);
                return tmp;
            }
            return null;
        }

        public void Push(T obj)
        {
            if (obj != null)
            {
                // note: don't want to add GC.SuppressFinalize
                // to Reset, in case Reset is called independently
                // of lifetime management
                if (recycled == null)
                {
                    obj.Reset(true);
                    GC.SuppressFinalize(obj);
                    recycled = obj;
                }
                else
                {
                    obj.Reset(false);
                    GC.SuppressFinalize(obj);
                }
            }
        }
    }

    internal static class Cache<T> where T : class, IRecyclable
    {
        [ThreadStatic]
        private static T recycled;

        public static T Pop()
        {
            var tmp = recycled;
            if (tmp != null)
            {
                recycled = null;
                GC.ReRegisterForFinalize(tmp);
                return tmp;
            }
            return null;
        }

        public static void Push(T obj)
        {
            if (obj != null)
            {
                // note: don't want to add GC.SuppressFinalize
                // to Reset, in case Reset is called independently
                // of lifetime management
                if (recycled == null)
                {
                    obj.Reset(true);
                    GC.SuppressFinalize(obj);
                    recycled = obj;
                }
                else
                {
                    obj.Reset(false);
                    GC.SuppressFinalize(obj);
                }
            }
        }
    }
}
