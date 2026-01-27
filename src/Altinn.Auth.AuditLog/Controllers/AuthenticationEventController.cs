using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Altinn.Auth.AuditLog.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Auth.AuditLog.Controllers
{
    /// <summary>
    /// Expose Api endpoints for authentication log
    /// </summary>
    [ApiController]
    public class AuthenticationEventController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IAuthenticationEventService _authenticationEventService;
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationEventController"/> class
        /// </summary>
        public AuthenticationEventController(
            ILogger<AuthenticationEventController> logger,
            IAuthenticationEventService authenticationEventService) 
        {
            _logger = logger;
            _authenticationEventService = authenticationEventService;
        }

        [HttpPost]
        [Route("auditlog/api/v1/authenticationevent")]
        [Consumes("application/json")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [ServiceFilter(typeof(ValidationFilterAttribute))]
        public async Task<ActionResult> Post([FromBody] AuthenticationEvent authenticationEvent)
        {
            try
            {
                await _authenticationEventService.CreateAuthenticationEvent(authenticationEvent);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Internal exception occurred during logging of authentication event");
                return new ObjectResult(ProblemDetailsFactory.CreateProblemDetails(HttpContext)) { StatusCode = StatusCodes.Status500InternalServerError };
            }

        }
    }
}
