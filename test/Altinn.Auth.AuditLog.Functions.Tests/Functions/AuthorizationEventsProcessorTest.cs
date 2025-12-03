using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Tests.Helpers;
using Moq;
using System.Buffers;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Functions.Tests.Functions;

public  class AuthorizationEventsProcessorTest
{
    [Fact]
    public async Task Run_LegacyContent_Base64DecodesAndForwardsToClient()
    {
        var authorizationEvent = TestDataHelper.GetAuthorizationEvent_LegacyFormat();
        Mock<IAuditLogClient> clientMock = CreateMockThatExpectsTestEvent();

        AuthorizationEventsProcessor sut = new AuthorizationEventsProcessor(clientMock.Object);

        // Act
        await sut.Run(authorizationEvent, null!, CancellationToken.None);

        // Assert
        clientMock.VerifyAll();
    }

    [Fact]
    public async Task Run_V1Content_BrotliDecidesAndForwardsToClient()
    {
        var authorizationEvent = TestDataHelper.GetAuthorizationEvent_V1Format();
        Mock<IAuditLogClient> clientMock = CreateMockThatExpectsTestEvent();

        AuthorizationEventsProcessor sut = new AuthorizationEventsProcessor(clientMock.Object);

        // Act
        await sut.Run(authorizationEvent, null!, CancellationToken.None);

        // Assert
        clientMock.VerifyAll();
    }

    [Fact]
    public async Task Run_TooSmallMessage_IsIgnored()
    {
        Mock<IAuditLogClient> clientMock = new();

        AuthorizationEventsProcessor sut = new AuthorizationEventsProcessor(clientMock.Object);

        // Act
        await sut.Run(new BinaryData([]), null!, CancellationToken.None);

        // Assert
        clientMock.Verify(c => c.SaveAuthorizationEvent(It.IsAny<ReadOnlySequence<byte>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_InvalidVersion_Throws()
    {
        Mock<IAuditLogClient> clientMock = new();

        AuthorizationEventsProcessor sut = new AuthorizationEventsProcessor(clientMock.Object);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Run(new BinaryData([.. "99abc"u8]), null!, CancellationToken.None));

        // Assert
        clientMock.Verify(c => c.SaveAuthorizationEvent(It.IsAny<ReadOnlySequence<byte>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Mock<IAuditLogClient> CreateMockThatExpectsTestEvent()
    {
        Mock<IAuditLogClient> clientMock = new();
        clientMock.Setup(c => c.SaveAuthorizationEvent(It.IsAny<ReadOnlySequence<byte>>(), It.IsAny<CancellationToken>()))
            .Callback((ReadOnlySequence<byte> data, CancellationToken cancellationToken) =>
            {
                var expectedAuthorizationEvent = TestDataHelper.GetAuthorizationEvent();
                var reader = new Utf8JsonReader(data);
                var actualAuthorizationEvent = JsonSerializer.Deserialize<AuthorizationEvent>(ref reader, JsonSerializerOptions.Web)!;

                Assert.Equal(expectedAuthorizationEvent.InstanceId, actualAuthorizationEvent.InstanceId);
                Assert.Equal(expectedAuthorizationEvent.Operation, actualAuthorizationEvent.Operation);
                Assert.Equal(expectedAuthorizationEvent.Resource, actualAuthorizationEvent.Resource);
                Assert.Equal(expectedAuthorizationEvent.IpAdress, actualAuthorizationEvent.IpAdress);
                Assert.Equal(expectedAuthorizationEvent.Created, actualAuthorizationEvent.Created);
                Assert.Equal(expectedAuthorizationEvent.ContextRequestJson, actualAuthorizationEvent.ContextRequestJson, JsonElement.DeepEquals);
            })
            .Returns(Task.CompletedTask);

        return clientMock;
    }
}
