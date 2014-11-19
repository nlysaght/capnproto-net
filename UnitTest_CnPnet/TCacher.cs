using CapnProto;
using CapnProto.Wrappers;
using Moq;
using Moq.Language;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest_CnPnet
{

    internal class FakeRecycable : IRecyclable
    {
        public bool IsRecycling { get; set; }

        public void Reset(bool recycling)
        {
            IsRecycling = recycling;
        }
        public void Dispose()
        {
        }
    }

    [TestFixture]
    public class TCacher
    {
        internal class TestFactory
        {
            public Mock<IGCWrapper> GCMock { get; private set; }
            public ScopedCache<IRecyclable> Cacher { get; private set; }
            public TestFactory()
            {
                GCMock = new Mock<IGCWrapper>(MockBehavior.Loose);
                Cacher = new ScopedCache<IRecyclable>(GCMock.Object);
            }
        }
        
        [Test]
        public void Construction_With_Null_GC_Fails()
        {
            Assert.Throws<ArgumentNullException>(() => { var cache = new ScopedCache<FakeRecycable>(null); });
        }
        [Test]
        public void Construction_With_GC_IsSuccess()
        {
            var factory = new TestFactory();
            Assert.IsNotNull(factory.Cacher.GC);
        }
        [Test]
        public void Pushing_A_Null_Has_No_Effect()
        {
            var factory = new TestFactory();
            factory.Cacher.Push(null);
            factory.GCMock.Verify(m => m.SuppressFinalize(It.IsAny<FakeRecycable>()), Times.Never());
            factory.GCMock.Verify(m => m.ReRegisterForFinalize(It.IsAny<FakeRecycable>()), Times.Never());
        }
        [Test]
        public void Pushing_A_Recyclable_Calls_Expected_Methods()
        {
            var factory = new TestFactory();
            var recycable = new Mock<IRecyclable>();
            factory.Cacher.Push(recycable.Object);
            factory.GCMock.Verify(m => m.SuppressFinalize(It.IsAny<IRecyclable>()), Times.Once());
            recycable.Verify(r => r.Reset(true), Times.Once());
        }
        [Test]
        public void Pushing_An_Existing_Recyclable_Calls_Expected_Methods()
        {
            var factory = new TestFactory();
            var recycableOne = new Mock<IRecyclable>();
            var recycableTwo = new Mock<IRecyclable>();
            factory.Cacher.Push(recycableOne.Object);
            factory.Cacher.Push(recycableTwo.Object);
            factory.GCMock.Verify(m => m.SuppressFinalize(It.IsAny<IRecyclable>()), Times.Exactly(2));
            recycableTwo.Verify(r => r.Reset(false), Times.Once());
        }
        [Test]
        public void Poping_A_Recyclable_Calls_Expected_Methods()
        {
            var factory = new TestFactory();
            var recycable = new Mock<IRecyclable>();
            factory.Cacher.Push(recycable.Object);
            var result = factory.Cacher.Pop();
            Assert.That(result, Is.Not.Null);
            factory.GCMock.Verify(m => m.ReRegisterForFinalize(It.IsAny<IRecyclable>()), Times.Once);
        }


    }
}
