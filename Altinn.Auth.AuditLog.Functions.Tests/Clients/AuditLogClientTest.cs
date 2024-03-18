using Altinn.Auth.AuditLog.Core.Enum;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Functions.Clients;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Configuration;
using Altinn.Auth.AuditLog.Functions.Tests.Helpers;
using Azure.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Functions.Tests.Clients
{
    public class AuditLogClientTest
    {
        Mock<ILogger<IAuditLogClient>> _loggerMock = new Mock<ILogger<IAuditLogClient>>();
        IOptions<PlatformSettings> _platformSettings = Options.Create(new PlatformSettings
        {
            AuditLogApiEndpoint = "https://platform.test.altinn.cloud/"
        });

        private readonly AuthenticationEvent authenticationEvent = new AuthenticationEvent()
        {
            UserId = 20000003,
            Created = DateTime.UtcNow,
            AuthenticationMethod = AuthenticationMethod.BankID,
            EventType = AuthenticationEventType.Authenticate,
            AuthenticationLevel = SecurityLevel.VerySensitive,
        };

        /// <summary>
        /// Verify that the endpoint the client sends a request to is set correctly
        /// </summary>
        [Fact]
        public async Task SaveAuthenticationEvent_SuccessResponse()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/auditlog/api/v1/authenticationevent",
                HttpStatusCode.OK);

            var client = new AuditLogClient(_loggerMock.Object, new HttpClient(handlerMock.Object), _platformSettings);
            // Act
            await client.SaveAuthenticationEvent(authenticationEvent);

            // Assert
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task SaveAuthenticationEvent_NonSuccessResponse_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/auditlog/api/v1/authenticationevent",
                HttpStatusCode.ServiceUnavailable);

            var client = CreateTestInstance(handlerMock.Object);

            // Act

            await Assert.ThrowsAsync<HttpRequestException>(async () => await client.SaveAuthenticationEvent(authenticationEvent));

            // Assert
            handlerMock.VerifyAll();
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SaveAuthenticationEvent failed with status code ServiceUnavailable")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Verify that the endpoint the client sends a request to is set correctly
        /// </summary>
        [Fact]
        public async Task SaveAuthorizationEvent_SuccessResponse()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/auditlog/api/v1/authorizationevent",
                HttpStatusCode.OK);

            var client = new AuditLogClient(_loggerMock.Object, new HttpClient(handlerMock.Object), _platformSettings);
            // Act
            await client.SaveAuthorizationEvent(TestDataHelper.GetAuthorizationEvent());

            // Assert
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task SaveAuthorizationEvent_NonSuccessResponse_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/auditlog/api/v1/authorizationevent",
                HttpStatusCode.ServiceUnavailable);

            var client = CreateTestInstance(handlerMock.Object);

            // Act

            await Assert.ThrowsAsync<HttpRequestException>(async () => await client.SaveAuthorizationEvent(TestDataHelper.GetAuthorizationEvent()));

            // Assert
            handlerMock.VerifyAll();
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SaveAuthorizationEvent failed with status code ServiceUnavailable")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        private static Mock<HttpMessageHandler> CreateMessageHandlerMock(string clientEndpoint, HttpStatusCode statusCode)
        {
            var messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            messageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(rm => rm.RequestUri.Equals(clientEndpoint)), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var response = new HttpResponseMessage(statusCode);
                    return response;
                })
                .Verifiable();

            return messageHandlerMock;
        }

        private AuditLogClient CreateTestInstance(HttpMessageHandler messageHandlerMock)
        {
            return new AuditLogClient(
                  _loggerMock.Object,
                  new HttpClient(messageHandlerMock),
                  _platformSettings);
        }        
    }
}
