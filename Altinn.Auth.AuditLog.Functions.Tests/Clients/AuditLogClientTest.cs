using Altinn.Auth.AuditLog.Functions.Clients;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Configuration;
using Altinn.Auth.AuditLog.Functions.Models;
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
            UserId = "20000003",
            Created = DateTime.UtcNow,
            AuthenticationMethod = "BankId",
            EventType = "LoggedIn",
            SessionId = "83343b4c-865d-4e6c-888d-33bc7533ea2d",
            AuthenticationLevel = "4",
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
            await client.SaveAuthorizationEvent(GetAuthorizationEvent());

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

            await Assert.ThrowsAsync<HttpRequestException>(async () => await client.SaveAuthorizationEvent(GetAuthorizationEvent()));

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

        private AuthorizationEvent GetAuthorizationEvent()
        {
            Models.Attribute userAttribute = new Models.Attribute()
            {
                Id = "urn:altinn:userid",
                Value = "1",
            };

            Models.Attribute role1Attribute = new Models.Attribute()
            {
                Id = "urn:altinn:role",
                Value = "dagl",
            };

            Models.Attribute role2Attribute = new Models.Attribute()
            {
                Id = "urn:altinn:role",
                Value = "utinn",
            };

           Models.Attribute actionAttribute = new Models.Attribute()
            {
                Id = "urn:oasis:names:tc:xacml:1.0:action:action-id",
                Value = "read",
                DataType = "http://www.w3.org/2001/XMLSchema#string"
            };

            Models.Attribute instanceAttribute = new Models.Attribute()
            {
                Id = "urn:altinn:instance-id",
                Value = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
                IncludeInResult = true
            };
            Models.Attribute orgAttribute = new Models.Attribute()
            {
                Id = "urn:altinn:org",
                Value = "skd"
            };
            Models.Attribute appAttribute = new Models.Attribute()
            {
                Id = "urn:altinn:app",
                Value = "taxreport"
            };
            Models.Attribute partyAttribute = new Models.Attribute()
            {
                Id = "urn:altinn:partyid",
                Value = "1000"
            };
            Models.Attribute taskAttribute = new Models.Attribute()
            {
                Id = "urn:altinn:task",
                Value = "formfilling"
            };

            AccessSubject accessSubject = new AccessSubject();
            accessSubject.Attribute = new List<Models.Attribute>();
            accessSubject.Attribute.Add(userAttribute);
            accessSubject.Attribute.Add(role1Attribute);
            accessSubject.Attribute.Add(role2Attribute);

            Models.Action action = new Models.Action();
            action.Attribute = new List<Models.Attribute>();
            action.Attribute.Add(actionAttribute);

            Models.Resource resource = new Models.Resource();
            resource.Attribute = new List<Models.Attribute>();
            resource.Attribute.Add(instanceAttribute);
            resource.Attribute.Add(orgAttribute);
            resource.Attribute.Add(appAttribute);
            resource.Attribute.Add(partyAttribute);
            resource.Attribute.Add(taskAttribute);


            ContextRequest contextRequest = new ContextRequest();
            contextRequest.AccessSubject = new List<AccessSubject>();
            contextRequest.Action = new List<Models.Action>();
            contextRequest.AccessSubject = new List<AccessSubject>();
            contextRequest.Resources = new List<Resource>();
            contextRequest.AccessSubject.Add(accessSubject);
            contextRequest.Action.Add(action);
            contextRequest.Resources.Add(resource);

            AuthorizationEvent authorizationEvent = new AuthorizationEvent()
            {
                SubjectUserId = "2000000",
                SubjectParty = "",
                ResourcePartyId = "1000",
                Resource = "taxreport",
                InstanceId = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
                Operation = "read",
                TimeToDelete = "",
                IpAdress = "192.0.2.1",
                ContextRequestJson = contextRequest

            };

            return authorizationEvent;
        }
    }
}
