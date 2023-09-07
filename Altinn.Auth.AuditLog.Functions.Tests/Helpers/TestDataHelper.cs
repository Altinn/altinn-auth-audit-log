using Altinn.Auth.AuditLog.Functions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Functions.Tests.Helpers
{
    public static class TestDataHelper
    {
        public static AuthorizationEvent GetAuthorizationEvent()
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
