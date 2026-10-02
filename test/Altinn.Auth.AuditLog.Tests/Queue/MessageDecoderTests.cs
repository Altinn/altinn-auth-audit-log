using Altinn.Auth.AuditLog.Core.Enum;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Queue;
using System.Text;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Tests.Queue;

/// <summary>
/// Ported from the function app tests (<c>AuthorizationEventsProcessorTest</c>, <c>EventsProcessorTest</c>):
/// every wire format the function app accepted must decode identically in-process.
/// </summary>
public class AuthorizationEventMessageDecoderTests
{
    [Fact]
    public void Decode_LegacyBase64_Decodes()
    {
        var actual = AuthorizationEventMessageDecoder.Decode(QueueTestData.GetAuthorizationEvent_LegacyFormat());

        AssertExpectedEvent(actual);
    }

    [Fact]
    public void Decode_LegacyRawJson_Decodes()
    {
        var actual = AuthorizationEventMessageDecoder.Decode(QueueTestData.GetAuthorizationEvent_LegacyFormat_NonBase64());

        AssertExpectedEvent(actual);
    }

    [Fact]
    public void Decode_V1Brotli_Decodes()
    {
        var actual = AuthorizationEventMessageDecoder.Decode(QueueTestData.GetAuthorizationEvent_V1Format());

        AssertExpectedEvent(actual);
    }

    [Fact]
    public void Decode_V1BrotliBase64Wrapped_Decodes()
    {
        var actual = AuthorizationEventMessageDecoder.Decode(QueueTestData.GetAuthorizationEvent_V1Format_Base64Wrapped());

        AssertExpectedEvent(actual);
    }

    [Fact]
    public void Decode_EnumAsString_Decodes()
    {
        var json = Encoding.UTF8.GetBytes("""{"created":"2018-05-15T02:05:00Z","operation":"read","decision":"Deny","contextRequestJson":{}}""");

        var actual = AuthorizationEventMessageDecoder.Decode(json);

        Assert.Equal(XacmlContextDecision.Deny, actual.Decision);
    }

    [Fact]
    public void Decode_TooSmall_ThrowsTooSmall()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthorizationEventMessageDecoder.Decode(Array.Empty<byte>()));

        Assert.Equal(MessageDecodeException.Reasons.TooSmall, ex.Reason);
    }

    [Fact]
    public void Decode_UnsupportedVersion_ThrowsUnsupportedVersion()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthorizationEventMessageDecoder.Decode("99abc"u8.ToArray()));

        Assert.Equal(MessageDecodeException.Reasons.UnsupportedVersion, ex.Reason);
    }

    [Fact]
    public void Decode_Garbage_ThrowsInvalidBase64()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthorizationEventMessageDecoder.Decode("!!!not base64 at all!!!"u8.ToArray()));

        Assert.Equal(MessageDecodeException.Reasons.InvalidBase64, ex.Reason);
    }

    [Fact]
    public void Decode_CorruptBrotli_IsPermanentFailure()
    {
        // Brotli may either reject the stream or decode it to junk; both are permanent (poison), the reason differs.
        var ex = Assert.Throws<MessageDecodeException>(() => AuthorizationEventMessageDecoder.Decode("01\xff\xfe\x00\x01garbage"u8.ToArray()));

        Assert.Contains(ex.Reason, new[] { MessageDecodeException.Reasons.InvalidCompression, MessageDecodeException.Reasons.InvalidJson });
    }

    [Fact]
    public void Decode_InvalidJson_ThrowsInvalidJson()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthorizationEventMessageDecoder.Decode("{not json"u8.ToArray()));

        Assert.Equal(MessageDecodeException.Reasons.InvalidJson, ex.Reason);
    }

    [Theory]
    [InlineData("""{"operation":"read","decision":0,"contextRequestJson":{}}""")] // missing created
    [InlineData("""{"created":"2018-05-15T02:05:00Z","operation":"read","contextRequestJson":{}}""")] // missing decision
    [InlineData("""{"created":"2018-05-15T02:05:00Z","operation":"","decision":0,"contextRequestJson":{}}""")] // empty operation
    [InlineData("""{"created":"2018-05-15T02:05:00Z","operation":"read","decision":0,"contextRequestJson":[]}""")] // context not an object
    public void Decode_FailsValidation_ThrowsValidationFailed(string json)
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthorizationEventMessageDecoder.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(MessageDecodeException.Reasons.ValidationFailed, ex.Reason);
    }

    private static void AssertExpectedEvent(AuthorizationEvent actual)
    {
        var expected = QueueTestData.GetAuthorizationEvent();

        Assert.Equal(expected.InstanceId, actual.InstanceId);
        Assert.Equal(expected.Operation, actual.Operation);
        Assert.Equal(expected.Resource, actual.Resource);
        Assert.Equal(expected.IpAdress, actual.IpAdress);
        Assert.Equal(expected.Created, actual.Created);
        Assert.Equal(expected.Decision, actual.Decision);
        Assert.Equal(expected.SubjectUserId, actual.SubjectUserId);
        Assert.Equal(expected.SubjectPartyUuid, actual.SubjectPartyUuid);
        Assert.Equal(expected.TraceId, actual.TraceId);
        Assert.Equal(expected.ContextRequestJson, actual.ContextRequestJson, JsonElement.DeepEquals);
    }
}

public class AuthenticationEventMessageDecoderTests
{
    [Fact]
    public void Decode_Json_Decodes()
    {
        var actual = AuthenticationEventMessageDecoder.Decode(QueueTestData.GetAuthenticationEvent_JsonData());

        AssertExpectedEvent(actual);
    }

    [Fact]
    public void Decode_Base64Wrapped_Decodes()
    {
        var actual = AuthenticationEventMessageDecoder.Decode(QueueTestData.GetAuthenticationEvent_Base64Wrapped());

        AssertExpectedEvent(actual);
    }

    [Fact]
    public void Decode_EnumAsNumber_Decodes()
    {
        var json = Encoding.UTF8.GetBytes("""{"created":"2023-09-07T06:24:43Z","eventType":2,"authenticationMethod":16}""");

        var actual = AuthenticationEventMessageDecoder.Decode(json);

        Assert.Equal(AuthenticationEventType.Refresh, actual.EventType);
        Assert.Equal(AuthenticationMethod.MaskinPorten, actual.AuthenticationMethod);
    }

    [Fact]
    public void Decode_TooSmall_ThrowsTooSmall()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthenticationEventMessageDecoder.Decode("{"u8.ToArray()));

        Assert.Equal(MessageDecodeException.Reasons.TooSmall, ex.Reason);
    }

    [Fact]
    public void Decode_Garbage_ThrowsInvalidBase64()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthenticationEventMessageDecoder.Decode("!!!"u8.ToArray()));

        Assert.Equal(MessageDecodeException.Reasons.InvalidBase64, ex.Reason);
    }

    [Fact]
    public void Decode_InvalidJson_ThrowsInvalidJson()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthenticationEventMessageDecoder.Decode("{\"created\":"u8.ToArray()));

        Assert.Equal(MessageDecodeException.Reasons.InvalidJson, ex.Reason);
    }

    [Fact]
    public void Decode_MissingCreated_ThrowsValidationFailed()
    {
        var ex = Assert.Throws<MessageDecodeException>(() => AuthenticationEventMessageDecoder.Decode("""{"userId":1}"""u8.ToArray()));

        Assert.Equal(MessageDecodeException.Reasons.ValidationFailed, ex.Reason);
    }

    private static void AssertExpectedEvent(AuthenticationEvent actual)
    {
        var expected = QueueTestData.GetAuthenticationEvent();

        Assert.Equal(expected.AuthenticationLevel, actual.AuthenticationLevel);
        Assert.Equal(expected.AuthenticationMethod, actual.AuthenticationMethod);
        Assert.Equal(expected.Created, actual.Created);
        Assert.Equal(expected.EventType, actual.EventType);
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.Equal(expected.IpAddress, actual.IpAddress);
        Assert.Equal(expected.IsAuthenticated, actual.IsAuthenticated);
        Assert.Equal(expected.SessionId, actual.SessionId);
    }
}
