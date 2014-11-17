using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapnProto.Wrappers
{
    /// <summary>
    /// Wrapper for garbage collection to enure behaviour can be mocked/tested.
    /// </summary>
    public interface IGCWrapper
    {
        void ReRegisterForFinalize(object obj);
        void SuppressFinalize(object obj);
    }
    internal class GCWrapper : IGCWrapper
    {
        public void ReRegisterForFinalize(object obj)
        {
            GC.ReRegisterForFinalize(obj);
        }
        public void SuppressFinalize(object obj)
        {
            GC.SuppressFinalize(obj);
        }
    }
}
