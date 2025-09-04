using Microsoft.AspNetCore.Mvc;
using FractalGpu.RenderServer.Models;
using FractalGpu.RenderServer.Services;
using FractalGpu.Rendering.Fractal;
using FractalGpu.Rendering.Common;
using System.ComponentModel.DataAnnotations;

namespace FractalGpu.RenderServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FractalController : ControllerBase
{
    private readonly IRenderQueue _renderQueue;
    private readonly ILogger<FractalController> _logger;

    public FractalController(IRenderQueue renderQueue, ILogger<FractalController> logger)
    {
        _renderQueue = renderQueue;
        _logger = logger;
    }

    /// <summary>
    /// Render a fractal image based on the provided parameters
    /// </summary>
    /// <param name="request">Fractal rendering parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered fractal as BMP image</returns>
    [HttpPost("render")]
    public async Task<IActionResult> RenderFractal([FromBody] FractalRequest request, CancellationToken cancellationToken = default)
    {
        // Validate the model
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid fractal request received");
            return BadRequest(ModelState);
        }

        try
        {
            _logger.LogInformation("Processing fractal render request: {Width}x{Height}, Pattern: {Pattern}",
                request.Width, request.Height, request.Pattern);

            // Convert API model to rendering settings
            var settings = new Lyapunov.Settings()
                .SetA(request.StartA, request.EndA)
                .SetB(request.StartB, request.EndB)
                .SetPattern(request.Pattern)
                .SetInitial(request.Initial)
                .SetIterations(request.Warmup, request.Iterations)
                .SetSize(new Sz(request.Width, request.Height))
                .SetContrast(request.Contrast);

            // Queue the render job
            var result = await _renderQueue.QueueRenderAsync(settings, cancellationToken);

            // Convert to byte array for HTTP response
            var imageBytes = result.ToByteArray();

            _logger.LogInformation("Successfully rendered fractal: {Width}x{Height}, Size: {Size} bytes",
                request.Width, request.Height, imageBytes.Length);

            // Return the bitmap image with proper MIME type
            return File(imageBytes, "image/bmp", $"fractal_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bmp");
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Render request timed out");
            return StatusCode(408, new { error = "Render request timed out", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Render queue is full or unavailable");
            return StatusCode(503, new { error = "Service temporarily unavailable", message = ex.Message });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Render request cancelled");
            return BadRequest(new { error = "Request cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render fractal");
            return StatusCode(500, new { error = "Internal server error", message = "Failed to process render request" });
        }
    }

    /// <summary>
    /// Get fractal rendering via GET request with query parameters
    /// </summary>
    /// <param name="fractalType">Fractal type (default: lyapunov)</param>
    /// <param name="width">Image width</param>
    /// <param name="height">Image height</param>
    /// <param name="startA">Start value for parameter A</param>
    /// <param name="endA">End value for parameter A</param>
    /// <param name="startB">Start value for parameter B</param>
    /// <param name="endB">End value for parameter B</param>
    /// <param name="initial">Initial value</param>
    /// <param name="pattern">Iteration pattern</param>
    /// <param name="warmup">Warmup iterations</param>
    /// <param name="iterations">Main iterations</param>
    /// <param name="contrast">Contrast value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered fractal as BMP image</returns>
    [HttpGet("render")]
    public async Task<IActionResult> RenderFractalGet(
        [FromQuery] string fractalType = "lyapunov",
        [FromQuery, Required, Range(1, 10000)] int width = 256,
        [FromQuery, Required, Range(1, 10000)] int height = 256,
        [FromQuery, Required] double startA = 2.0,
        [FromQuery, Required] double endA = 4.0,
        [FromQuery, Required] double startB = 2.0,
        [FromQuery, Required] double endB = 4.0,
        [FromQuery, Required, Range(0.0, 1.0)] double initial = 0.5,
        [FromQuery, Required] string pattern = "ab",
        [FromQuery, Required, Range(1, 1000)] int warmup = 10,
        [FromQuery, Required, Range(1, 1000000)] int iterations = 1000,
        [FromQuery, Required, Range(0.1, 10.0)] double contrast = 1.7,
        CancellationToken cancellationToken = default)
    {
        // Create request object from query parameters
        var request = new FractalRequest
        {
            FractalType = fractalType,
            Width = width,
            Height = height,
            StartA = startA,
            EndA = endA,
            StartB = startB,
            EndB = endB,
            Initial = initial,
            Pattern = pattern,
            Warmup = warmup,
            Iterations = iterations,
            Contrast = contrast
        };

        // Validate the model
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, context, results, true))
        {
            var errors = results.Select(r => r.ErrorMessage).ToArray();
            _logger.LogWarning("Invalid fractal GET request parameters: {Errors}", string.Join(", ", errors));
            return BadRequest(new { errors });
        }

        // Use the same logic as POST
        return await RenderFractal(request, cancellationToken);
    }

    /// <summary>
    /// Get current queue status
    /// </summary>
    /// <returns>Queue status information</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var status = _renderQueue.GetQueueStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue status");
            return StatusCode(500, new { error = "Failed to get queue status" });
        }
    }
}