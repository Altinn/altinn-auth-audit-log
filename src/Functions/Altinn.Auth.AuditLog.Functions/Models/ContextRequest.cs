using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Auth.AuditLog.Functions.Models
{
    /// <summary>
    /// This model describes an authorization event. An authorization event is an action triggered when a user requests access to an operation
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AccessSubject
    {
        public List<Attribute> Attribute { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class Action
    {
        public List<Attribute> Attribute { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class Resource
    {
        public List<Attribute> Attribute { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class Attribute
    {
        public string Id { get; set; }
        public string Value { get; set; }
        public string DataType { get; set; }
        public bool IncludeInResult { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ContextRequest
    {
        public bool ReturnPolicyIdList { get; set; }
        public List<AccessSubject> AccessSubject { get; set; }
        public List<Action> Action { get; set; }
        public List<Resource> Resources { get; set; }
    }
}
