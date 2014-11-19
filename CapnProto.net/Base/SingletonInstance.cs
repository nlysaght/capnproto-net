using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapnProto.Base
{
    /// <summary>
    /// Demand that only a single instance of a class exists.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SingletonInstance<T> where T : class
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
                        throw new SingleInstanceOnlyException<T>();
                    }
                    singleInstance = this;
                }
            }
            else
            {
                throw new SingleInstanceOnlyException<T>();
            }
        }
    }

    public class SingleInstanceOnlyException<T> : Exception
    {
        public SingleInstanceOnlyException()
            : base(string.Format("You are only allowed a single instance of this class {0}", typeof(T).FullName))
        {
        }
    }

}
