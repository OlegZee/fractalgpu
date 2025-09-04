using System.ComponentModel.DataAnnotations;

namespace FractalGpu.RenderServer.Models;

public class FractalRequest
{
    [Required]
    public string FractalType { get; set; } = "lyapunov";

    [Required]
    [Range(1, 10000)]
    public int Width { get; set; }

    [Required]
    [Range(1, 10000)]
    public int Height { get; set; }

    [Required]
    public double StartA { get; set; }

    [Required]
    public double EndA { get; set; }

    [Required]
    public double StartB { get; set; }

    [Required]
    public double EndB { get; set; }

    [Required]
    [Range(0.0, 1.0)]
    public double Initial { get; set; }

    [Required]
    public string Pattern { get; set; } = "ab";

    [Required]
    [Range(1, 1000)]
    public int Warmup { get; set; }

    [Required]
    [Range(1, 1000000)]
    public int Iterations { get; set; }

    [Required]
    [Range(0.1, 10.0)]
    public double Contrast { get; set; }
}