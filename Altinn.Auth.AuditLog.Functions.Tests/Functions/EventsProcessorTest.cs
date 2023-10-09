using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Models;
using Azure.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Functions.Tests.Functions
{
    public  class EventsProcessorTest
    {
        Mock<ILogger<EventsProcessor>> _loggerMock = new Mock<ILogger<EventsProcessor>>();

        [Fact]
        public async Task Run_ConfirmDeserializationOfAuthenticationEvent()
        {

            // Arrange
            string serializedAuthenticationEvent = "{\"Created\":\"2023-09-07T06:24:43.971899Z\",\"UserId\":20000003,\"EventType\":\"LoggedIn\",\"AuthenticationMethod\":\"BankId\",\"AuthenticationLevel\":\"4\"}\r\n";

            AuthenticationEvent expectedAuthenticationEvent = new AuthenticationEvent()
            {
                UserId = 20000003,
                Created = DateTimeOffset.Parse("2023-09-07T06:24:43.971899Z").UtcDateTime,
                AuthenticationMethod = "BankId",
                EventType = "LoggedIn",
                IpAdress = "192.168.1.0.1",
                AuthenticationLevel = "4",
            };

        Mock<IAuditLogClient> clientMock = new();
            clientMock.Setup(c => c.SaveAuthenticationEvent(It.Is<AuthenticationEvent>(c => AssertExpectedAuthenticationEvent(c, expectedAuthenticationEvent))))
                .Returns(Task.CompletedTask);

            EventsProcessor sut = new EventsProcessor(_loggerMock.Object, clientMock.Object);

            // Act
            await sut.Run(serializedAuthenticationEvent, null);

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
            return true;
        }
    }
}
