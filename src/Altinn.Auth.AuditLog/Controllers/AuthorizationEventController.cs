﻿using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Altinn.Auth.AuditLog.Filters;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Auth.AuditLog.Controllers
{
    /// <summary>
    /// Expose Api endpoints for authorization event log
    /// </summary>
    [ApiController]
    public class AuthorizationEventController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IAuthorizationEventService _authorizationEventService;
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationEventController"/> class
        /// </summary>
        public AuthorizationEventController(
            ILogger<AuthorizationEventController> logger,
            IAuthorizationEventService authorizationEventService)
        {
            _logger = logger;
            _authorizationEventService = authorizationEventService;
        }

        [HttpPost]
        [Route("auditlog/api/v1/authorizationevent")]
        [Consumes("application/json")]
        [Produces("application/json")]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [ServiceFilter(typeof(ValidationFilterAttribute))]
        public async Task<ActionResult> Post([FromBody] AuthorizationEvent authorizationEvent)
        {
            try
            {
                AuthorizationEvent response = await _authorizationEventService.CreateAuthorizationEvent(authorizationEvent);
                return Created("/auditlog/api/v1/authorizationevent/", authorizationEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Internal exception occurred during maskinportenschema delegation");
                return new ObjectResult(ProblemDetailsFactory.CreateProblemDetails(HttpContext));
            }

        }
    }
}
