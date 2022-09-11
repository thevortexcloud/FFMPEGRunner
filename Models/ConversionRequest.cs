using System.IO;

namespace Cake.FFMpegRunner.Models {
    internal sealed record ConversionRequest {
        public FileInfo InputFile { get; init; }
        public DirectoryInfo OutputLocatation { get; init; }
        public ConversionType Type { get; init; } = ConversionType.Video;

        public enum ConversionType {
            Video,
            Subtitle
        }
    }
}