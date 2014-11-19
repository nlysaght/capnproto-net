using CapnProto;
using CapnProto.Providers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitTest_CnPnet.Base;

namespace UnitTest_CnPnet
{
    public class TBufferProviders : BasicTestFixture
    {
        [Test]
        public void Can_Create_A_Buffer_Provider()
        {
            var instance = new ManagedBufferProvider(10);
            Assert.That(instance.Buffer.Length, Is.EqualTo(10));
        }
        [Test]
        public void Can_Create_A_Unmanged_Provider()
        {
            var i = new UnmanagedBufferProvider(4);
            Assert.That(i, Is.Not.Null);
        }
    }
}
