using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpamClassifierAPI.Models.Request;
using SpamClassifierAPI.Services;

namespace SpamClassifierAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SpamController : ControllerBase
    {
        private readonly SpamDetectionService _service;

        public SpamController(SpamDetectionService service)
        {
            _service = service;
        }

        //TRAIN MODEL
        [HttpPost("train")]
        public async Task<IActionResult> Train([FromQuery] bool forceRetrain = false)
        {
            try
            {
                await _service.TrainModelAsync(forceRetrain);

                return Ok(new
                {
                    message = forceRetrain ? "Model re-trained from latest CSV successfully" : "Model trained successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    error = ex.Message
                });
            }
        }

        //PREDICT SPAM
        [HttpPost("predict")]
        public async Task<IActionResult> Predict([FromBody] PredictRequest request)
        {
            try
            {
                // Validation
                if (request == null || string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new
                    {
                        error = "Message cannot be empty"
                    });
                }

                var result = await _service.PredictAsync(request.Message);

                return Ok(new
                {
                    isSpam = result.Prediction,
                    probability = result.Probability,
                    score = result.Score,
                    confidence = result.Probability
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    error = ex.Message
                });
            }
        }
    }
}

