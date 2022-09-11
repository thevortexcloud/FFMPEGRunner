namespace Cake.FFMpegRunner.Models {
    internal sealed record Conversion {
        public int Progress { get; set; }
        public string FileName { get; init; }
        public string Destination { get; init; }
        public string FFMpegArguments { get; init; }
    }
}