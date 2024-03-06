using System;
using System.Collections.Generic;
using System.Text;

namespace Altinn.Auth.AuditLog.Core.Enum
{
    /// <summary>
    /// The <Decision /> element contains the result of policy evaluation.
    ///
    /// The <Decision /> element is of DecisionType simple type.
    /// The values of the<Decision /> element have the following meanings:
    /// “Permit”: the requested access is permitted
    /// “Deny”: the requested access is denied.
    /// “Indeterminate”: the PDP is unable to evaluate the requested access.  Reasons for such inability include: missing attributes, network errors while
    /// retrieving policies, division by zero during policy evaluation, syntax errors in the decision request or in the policy, etc.
    /// “NotApplicable”: the PDP does not have any policy that applies to this decision request.
    /// </summary>
    public enum XacmlContextDecision
    {
        /// <summary>
        /// “Permit”: the requested access is permitted
        /// </summary>
        Permit = 1,

        /// <summary>
        /// “Deny”: the requested access is denied.
        /// </summary>
        Deny = 2,

        /// <summary>
        /// “Indeterminate”: the PDP is unable to evaluate the requested access.  Reasons for such inability include: missing attributes, network errors while retrieving policies, division by zero during policy evaluation, syntax errors in the decision request or in the policy, etc.
        /// </summary>
        Indeterminate = 3,

        /// <summary>
        ///  “NotApplicable”: the PDP does not have any policy that applies to this decision request.
        /// </summary>
        NotApplicable = 4,
    }
}
