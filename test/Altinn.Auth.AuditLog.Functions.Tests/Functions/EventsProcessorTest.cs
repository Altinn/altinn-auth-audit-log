using Altinn.Auth.AuditLog.Core.Enum;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Auth.AuditLog.Functions.Tests.Functions
{
    public  class EventsProcessorTest
    {
        [Fact]
        public async Task Run_ConfirmDeserializationOfAuthenticationEvent()
        {

            // Arrange
            string serializedAuthenticationEvent = "{\"Created\":\"2023-09-07T06:24:43.971899Z\",\"UserId\":20000003,\"EventType\":\"Authenticate\",\"AuthenticationMethod\":\"BankId\",\"AuthenticationLevel\":\"VerySensitive\"}\r\n";

            AuthenticationEvent expectedAuthenticationEvent = new AuthenticationEvent()
            {
                UserId = 20000003,
                Created = DateTimeOffset.Parse("2023-09-07T06:24:43.971899Z").UtcDateTime,
                AuthenticationMethod = AuthenticationMethod.BankID,
                EventType = AuthenticationEventType.Authenticate,
                AuthenticationLevel = SecurityLevel.VerySensitive,
            };

        Mock<IAuditLogClient> clientMock = new();
            clientMock.Setup(c => c.SaveAuthenticationEvent(It.Is<AuthenticationEvent>(c => AssertExpectedAuthenticationEvent(c, expectedAuthenticationEvent)), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            EventsProcessor sut = new EventsProcessor(new NullLogger<EventsProcessor>(), clientMock.Object);

            // Act
            await sut.Run(serializedAuthenticationEvent, null!, CancellationToken.None);

            // Assert

            clientMock.VerifyAll();
        }

        private static bool AssertExpectedAuthenticationEvent(AuthenticationEvent actualAuthenticationEvent, AuthenticationEvent expectedAuthenticationEvent)
        {
            Assert.Equal(expectedAuthenticationEvent.AuthenticationLevel, actualAuthenticationEvent.AuthenticationLevel);
            Assert.Equal(expectedAuthenticationEvent.AuthenticationMethod, actualAuthenticationEvent.AuthenticationMethod);
            Assert.Equal(expectedAuthenticationEvent.Created, actualAuthenticationEvent.Created);
            Assert.Equal(expectedAuthenticationEvent.EventType, actualAuthenticationEvent.EventType);
            Assert.Equal(expectedAuthenticationEvent.OrgNumber, actualAuthenticationEvent.OrgNumber);
            Assert.Equal(expectedAuthenticationEvent.SupplierId, actualAuthenticationEvent.SupplierId);
            Assert.Equal(expectedAuthenticationEvent.UserId, actualAuthenticationEvent.UserId);
            Assert.Equal(expectedAuthenticationEvent.IpAddress, actualAuthenticationEvent.IpAddress);
            return true;
        }
    }
}
