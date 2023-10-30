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
            string serializedAuthorizationEvent = "{" +
                "\"Created\":\"0001-01-01T00:00:00\",\"SubjectOrgCode\":\"skd\"," +
                "\"ResourcePartyId\":1000,\"Resource\":\"taxreport\",\"InstanceId\":\"1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713\"," +
                "\"Operation\":\"read\",\"IpAdress\":\"192.0.2.1\"," +
                "\"ContextRequestJson\":\"{\\u0022ReturnPolicyIdList\\u0022:false,\\u0022CombinedDecision\\u0022:false,\\u0022XPathVersion\\u0022:null,\\u0022Attributes\\u0022:[{\\u0022Id\\u0022:null,\\u0022Content\\u0022:null,\\u0022Attributes\\u0022:[{\\u0022Issuer\\u0022:null,\\u0022AttributeId\\u0022:\\u0022urn:altinn:org\\u0022,\\u0022IncludeInResult\\u0022:false,\\u0022AttributeValues\\u0022:[{\\u0022Value\\u0022:\\u0022skd\\u0022,\\u0022DataType\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022Attributes\\u0022:[{\\u0022IsNamespaceDeclaration\\u0022:false,\\u0022Name\\u0022:{\\u0022LocalName\\u0022:\\u0022DataType\\u0022,\\u0022Namespace\\u0022:{\\u0022NamespaceName\\u0022:\\u0022\\u0022},\\u0022NamespaceName\\u0022:\\u0022\\u0022},\\u0022NextAttribute\\u0022:null,\\u0022NodeType\\u0022:2,\\u0022PreviousAttribute\\u0022:null,\\u0022Value\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022BaseUri\\u0022:\\u0022\\u0022,\\u0022Document\\u0022:null,\\u0022Parent\\u0022:null}],\\u0022Elements\\u0022:[]}]}],\\u0022Category\\u0022:\\u0022urn:oasis:names:tc:xacml:1.0:subject-category:access-subject\\u0022},{\\u0022Id\\u0022:null,\\u0022Content\\u0022:null,\\u0022Attributes\\u0022:[{\\u0022Issuer\\u0022:null,\\u0022AttributeId\\u0022:\\u0022urn:altinn:instance-id\\u0022,\\u0022IncludeInResult\\u0022:false,\\u0022AttributeValues\\u0022:[{\\u0022Value\\u0022:\\u00221000/26133fb5-a9f2-45d4-90b1-f6d93ad40713\\u0022,\\u0022DataType\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022Attributes\\u0022:[{\\u0022IsNamespaceDeclaration\\u0022:false,\\u0022Name\\u0022:{\\u0022LocalName\\u0022:\\u0022DataType\\u0022,\\u0022Namespace\\u0022:{\\u0022NamespaceName\\u0022:\\u0022\\u0022},\\u0022NamespaceName\\u0022:\\u0022\\u0022},\\u0022NextAttribute\\u0022:null,\\u0022NodeType\\u0022:2,\\u0022PreviousAttribute\\u0022:null,\\u0022Value\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022BaseUri\\u0022:\\u0022\\u0022,\\u0022Document\\u0022:null,\\u0022Parent\\u0022:null}],\\u0022Elements\\u0022:[]}]},{\\u0022Issuer\\u0022:null,\\u0022AttributeId\\u0022:\\u0022urn:altinn:org\\u0022,\\u0022IncludeInResult\\u0022:false,\\u0022AttributeValues\\u0022:[{\\u0022Value\\u0022:\\u0022skd\\u0022,\\u0022DataType\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022Attributes\\u0022:[],\\u0022Elements\\u0022:[]}]},{\\u0022Issuer\\u0022:null,\\u0022AttributeId\\u0022:\\u0022urn:altinn:app\\u0022,\\u0022IncludeInResult\\u0022:false,\\u0022AttributeValues\\u0022:[{\\u0022Value\\u0022:\\u0022taxreport\\u0022,\\u0022DataType\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022Attributes\\u0022:[],\\u0022Elements\\u0022:[]}]},{\\u0022Issuer\\u0022:null,\\u0022AttributeId\\u0022:\\u0022urn:altinn:task\\u0022,\\u0022IncludeInResult\\u0022:false,\\u0022AttributeValues\\u0022:[{\\u0022Value\\u0022:\\u0022Task_1\\u0022,\\u0022DataType\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022Attributes\\u0022:[],\\u0022Elements\\u0022:[]}]},{\\u0022Issuer\\u0022:null,\\u0022AttributeId\\u0022:\\u0022urn:altinn:partyid\\u0022,\\u0022IncludeInResult\\u0022:true,\\u0022AttributeValues\\u0022:[{\\u0022Value\\u0022:\\u00221000\\u0022,\\u0022DataType\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022Attributes\\u0022:[],\\u0022Elements\\u0022:[]}]}],\\u0022Category\\u0022:\\u0022urn:oasis:names:tc:xacml:3.0:attribute-category:resource\\u0022},{\\u0022Id\\u0022:null,\\u0022Content\\u0022:null,\\u0022Attributes\\u0022:[{\\u0022Issuer\\u0022:null,\\u0022AttributeId\\u0022:\\u0022urn:oasis:names:tc:xacml:1.0:action:action-id\\u0022,\\u0022IncludeInResult\\u0022:false,\\u0022AttributeValues\\u0022:[{\\u0022Value\\u0022:\\u0022read\\u0022,\\u0022DataType\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022Attributes\\u0022:[{\\u0022IsNamespaceDeclaration\\u0022:false,\\u0022Name\\u0022:{\\u0022LocalName\\u0022:\\u0022DataType\\u0022,\\u0022Namespace\\u0022:{\\u0022NamespaceName\\u0022:\\u0022\\u0022},\\u0022NamespaceName\\u0022:\\u0022\\u0022},\\u0022NextAttribute\\u0022:null,\\u0022NodeType\\u0022:2,\\u0022PreviousAttribute\\u0022:null,\\u0022Value\\u0022:\\u0022http://www.w3.org/2001/XMLSchema#string\\u0022,\\u0022BaseUri\\u0022:\\u0022\\u0022,\\u0022Document\\u0022:null,\\u0022Parent\\u0022:null}],\\u0022Elements\\u0022:[]}]}],\\u0022Category\\u0022:\\u0022urn:oasis:names:tc:xacml:3.0:attribute-category:action\\u0022},{\\u0022Id\\u0022:null,\\u0022Content\\u0022:null,\\u0022Attributes\\u0022:[],\\u0022Category\\u0022:\\u0022urn:oasis:names:tc:xacml:3.0:attribute-category:environment\\u0022}],\\u0022RequestReferences\\u0022:[]}\"}";

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
            Assert.Equal(expectedAuthorizationEvent.Created, actualAuthorizationEvent.Created);
            Assert.Equal(expectedAuthorizationEvent.ContextRequestJson, actualAuthorizationEvent.ContextRequestJson);            
            return true;
        }
    }
}
