using FileUploader.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace FileUploader.Worker_uTest
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public void Test() 
        {
            //Arrange
            var loggerMock = new Mock<ILogger<WorkerService>>();
            var connectionMock = new Mock<IConnectionMultiplexer>();
            var sut = new WorkerService(loggerMock.Object, connectionMock.Object);

            //Act & Assert
            Assert.Pass();
        }
    }
}
