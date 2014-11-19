using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using CapnProto;
using Moq;
using CapnProto.Wrappers;
using System.IO;

namespace UnitTest_CnPnet
{
    public class TBufferedStreamSegmentFactory : Base.BasicTestFixture
    {

        internal class TestHolder
        {
            public Mock<IGCWrapper> GCMock { get; private set; }
            public IBufferedStreamSegmentFactoryBuilder SegmentFactoryBuilder { get; set; }
            public TestHolder()
            {
                GCMock = new Mock<IGCWrapper>(MockBehavior.Loose);
                SegmentFactoryBuilder = new BufferedStreamSegmentFactoryBuilder(GCMock.Object);
            }
        }

        TestHolder holder = new TestHolder();

        [TestFixtureSetUp]
        public void Setup()
        {
        }
        [Test]
        public void Null_Stream_Throws_Exception()
        {
            Assert.Throws<ArgumentNullException>( () => { holder.SegmentFactoryBuilder.Create(null, 0 , false); } );
        }
        [Test]
        public void Can_A_Create_A_Valid_Stream_Factory()
        {
            var bytes = new byte[8];
            var instance = holder.SegmentFactoryBuilder.Create(new MemoryStream(bytes), bytes.Length, false);
            Assert.IsNotNull(instance);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Can_Read_A_Word_From_A_Stream()
        {
            // TODO:: Implements tests for the SegmentFactory abstract before testing here.
            uint value = 12345678;
            var bytes = BitConverter.GetBytes(value);
            var instance = holder.SegmentFactoryBuilder.Create(new MemoryStream(bytes), bytes.Length, false);
            Assert.IsNotNull(instance);
        }
    
    }
}
