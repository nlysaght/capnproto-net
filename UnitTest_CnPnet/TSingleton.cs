using CapnProto.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest_CnPnet
{
    [TestFixture]
    public class TSingleton
    {
        internal class SingletonTestOne : SingletonInstance<SingletonTestOne>
        {

        }
        internal class SingletonTestTwo : SingletonInstance<SingletonTestTwo>
        {

        }
        [Test]
        public void Can_Create_A_Single_Instance()
        {
            var instance = new SingletonTestOne();
            Assert.IsNotNull(instance);
            Assert.IsNotNull(SingletonTestOne.Instance);
            Assert.That(instance, Is.SameAs(SingletonTestOne.Instance));
        }
        [Test]
        public void Not_Allowed_To_Create_Multiple_Instances()
        {
            var instance01 = new SingletonTestTwo();
            Assert.Throws<SingleInstanceOnlyException<SingletonTestTwo>>(() => { var instance02 = new SingletonTestTwo(); });
        }
    }
}
