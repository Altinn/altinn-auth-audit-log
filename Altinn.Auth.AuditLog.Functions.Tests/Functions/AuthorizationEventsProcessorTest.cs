using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Models;
using Altinn.Auth.AuditLog.Functions.Tests.Helpers;
using Altinn.Auth.AuditLog.Functions.Tests.Utils;
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
    public  class AuthorizationEventsProcessorTest
    {
        Mock<ILogger<AuthorizationEventsProcessor>> _loggerMock = new Mock<ILogger<AuthorizationEventsProcessor>>();

        [Fact]
        public async Task Run_ConfirmDeserializationOfAuthorizationEvent()
        {

            // Arrange
            string serializedAuthorizationEvent = "{" +
                "\"TimeStamp\":\"0001-01-01T00:00:00\",\"SubjectUserId\":\"2000000\",\"SubjectParty\":\"\"," +
                "\"ResourcePartyId\":\"1000\",\"Resource\":\"taxreport\",\"InstanceId\":\"1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713\"," +
                "\"Operation\":\"read\",\"TimeToDelete\":\"\",\"IpAdress\":\"192.0.2.1\"," +
                "\"ContextRequestJson\":{\"ReturnPolicyIdList\":false," +
                "\"AccessSubject\":[{\"Attribute\":[{\"Id\":\"urn:altinn:userid\",\"Value\":\"1\",\"DataType\":null,\"IncludeInResult\":false}," +
                "{\"Id\":\"urn:altinn:role\",\"Value\":\"dagl\",\"DataType\":null,\"IncludeInResult\":false}," +
                "{\"Id\":\"urn:altinn:role\",\"Value\":\"utinn\",\"DataType\":null,\"IncludeInResult\":false}]}]," +
                "\"Action\":[{\"Attribute\":[{\"Id\":\"urn:oasis:names:tc:xacml:1.0:action:action-id\",\"Value\":\"read\"," +
                "\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"IncludeInResult\":false}]}]," +
                "\"Resources\":[{\"Attribute\":[{\"Id\":\"urn:altinn:instance-id\",\"Value\":\"1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713\"," +
                "\"DataType\":null,\"IncludeInResult\":true},{\"Id\":\"urn:altinn:org\",\"Value\":\"skd\"," +
                "\"DataType\":null,\"IncludeInResult\":false},{\"Id\":\"urn:altinn:app\",\"Value\":\"taxreport\"," +
                "\"DataType\":null,\"IncludeInResult\":false},{\"Id\":\"urn:altinn:partyid\",\"Value\":\"1000\"," +
                "\"DataType\":null,\"IncludeInResult\":false},{\"Id\":\"urn:altinn:task\",\"Value\":\"formfilling\"," +
                "\"DataType\":null,\"IncludeInResult\":false}]}]}}\r\n";

            Mock<IAuditLogClient> clientMock = new();
            clientMock.Setup(c => c.SaveAuthorizationEvent(It.Is<AuthorizationEvent>(c => AssertExpectedAuthorizationEvent(c, TestDataHelper.GetAuthorizationEvent()))))
                .Returns(Task.CompletedTask);

            AuthorizationEventsProcessor sut = new AuthorizationEventsProcessor(clientMock.Object);

            // Act
            await sut.Run(serializedAuthorizationEvent, null);

            // Assert

            clientMock.VerifyAll();
        }

        private static bool AssertExpectedAuthorizationEvent(AuthorizationEvent actualAuthorizationEvent, AuthorizationEvent expectedAuthorizationEvent)
        {
            Assert.Equal(expectedAuthorizationEvent.InstanceId, actualAuthorizationEvent.InstanceId);
            Assert.Equal(expectedAuthorizationEvent.Operation, actualAuthorizationEvent.Operation);
            Assert.Equal(expectedAuthorizationEvent.Resource, actualAuthorizationEvent.Resource);
            Assert.Equal(expectedAuthorizationEvent.IpAdress, actualAuthorizationEvent.IpAdress);
            Assert.Equal(expectedAuthorizationEvent.TimeToDelete, actualAuthorizationEvent.TimeToDelete);
            Assert.Equal(expectedAuthorizationEvent.TimeStamp, actualAuthorizationEvent.TimeStamp);
            AssertionUtil.AssertCollections(expectedAuthorizationEvent.ContextRequestJson.Action, actualAuthorizationEvent.ContextRequestJson.Action, AssertionUtil.AssertRuleEqual);
            AssertionUtil.AssertCollections(expectedAuthorizationEvent.ContextRequestJson.AccessSubject, actualAuthorizationEvent.ContextRequestJson.AccessSubject, AssertionUtil.AssertRuleEqual);
            AssertionUtil.AssertCollections(expectedAuthorizationEvent.ContextRequestJson.Resources, actualAuthorizationEvent.ContextRequestJson.Resources, AssertionUtil.AssertRuleEqual);
            Assert.Equal(expectedAuthorizationEvent.ContextRequestJson.ReturnPolicyIdList, actualAuthorizationEvent.ContextRequestJson.ReturnPolicyIdList);
            return true;
        }
    }
}
