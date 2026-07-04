using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.DTOs
{
    public class GenerationRequest
    {
        [Required]
        public string Prompt { get; set; }

        public string NegativePrompt { get; set; } = "";

        [Range(1, 10)]
        public int Steps { get; set; } = 20;

        [Range(0.1, 30.0)]
        public double CfgScale { get; set; } = 7.0;

        [Range(512, 2048)]
        public int Width { get; set; } = 512;

        [Range(512, 2048)]
        public int Height { get; set; } = 512;

        public long Seed { get; set; } = -1;
    }
}
