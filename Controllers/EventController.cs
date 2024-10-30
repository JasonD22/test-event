using ETMP.Models.Events;
using Events.API.Application.Queries;
using Events.API.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Events.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : BaseController
    {
        private readonly ILogger<EventsController> _logger;
        private readonly IEventQueries _eventQueries;

        public EventsController(ILogger<EventsController> logger, [FromServices] IEventQueries eventQueries, IConfiguration configuration)
            : base(configuration)
        {
            _logger = logger;
            _eventQueries = eventQueries;

            Console.WriteLine($"Event API Using redis = {_usingRedis}");
        }

        [HttpGet]
        [OutputCache(PolicyName = "Events")]
        public async Task<IActionResult> GetEvents()
        {
            try
            {
                var events = await RetreiveModelAsync<IList<EventResponseModel>>($"{RedisKeys.EVENTS}", () => _eventQueries.GetEventsDapperAsync());

                return Ok(events);
            }
            catch (Exception ex) when (ex is not KeyNotFoundException)
            {
                Console.WriteLine($"GetEvents Error - = {ex.Message}");
            }
            return NotFound();
        }

        [HttpGet]
        [Route("/api/Events/health")]
        public IActionResult Health()
        {
            return Ok();
        }

        [HttpGet]
        [Route("{id}")]
        [OutputCache(PolicyName = "Event")]
        public async Task<IActionResult> GetEvent(long id)
        {
            try
            {
                var e = await RetreiveModelAsync<EventResponseModel>($"{RedisKeys.EVENT}{id}", () => _eventQueries.GetEventDapperAsync(id));

                return Ok(e);
            }
            catch (Exception ex) when (ex is not KeyNotFoundException)
            {
                Console.WriteLine($"GetEvent Error - EventId = {ex.Message}");
            }
            return NotFound();
        }
        [HttpGet]
        [Route("{id}/{sessionId}")]
        [OutputCache(PolicyName = "Session")]
        public async Task<IActionResult> GetSession(long id, long sessionId)
        {
            try
            {
                var session = await RetreiveModelAsync<SessionResponseModel>(
                          $"{RedisKeys.SESSION}{id}_{sessionId}",
                          () => _eventQueries.GetSessionDapperAsync(id, sessionId));

                return Ok(session);
            }
            catch (Exception ex) when (ex is not KeyNotFoundException)
            {
                Console.WriteLine($"GetSession Error - EventId = {ex.Message}");
            }
            return NotFound();
        }
    }
}