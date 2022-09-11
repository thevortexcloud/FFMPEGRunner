using System;
using CommandLine;
using System.IO;

namespace Cake.FFMpegRunner.Models {
    internal sealed record Configuration {
        [Option('i', "input", Required = true, HelpText = "The input directory to run FFMpeg over")]
        public string InputPath { get; init; }

        [Option('p', "pattern", Required = true, HelpText = "The pattern used to find files to transcode in the input directory")]
        public string InputSearchPattern { get; init; }

        [Option('o', "output", Required = true, HelpText = "The output directory to save transcoded files to")]
        public string OutputPath { get; init; }

        [Option('s', "simulate", Required = false, HelpText = "This simulates running without actually transcoding any files")]
        public bool SimulateMode { get; init; }

        [Option('t', "type", Required = false, HelpText = "the type of transcode to do. Valid options are 'Subtitle' and 'Video'. Subtitle transcodes subtitles only.")]
        public ConversionRequest.ConversionType ConversionType { get; init; }

        public DirectoryInfo InputDirectory {
            get {
                if (InputDirectoryCache != null) {
                    return this.InputDirectoryCache;
                } else if (!string.IsNullOrWhiteSpace(this.InputPath)) {
                    this.InputDirectoryCache = new DirectoryInfo(this.InputPath);
                    return this.InputDirectoryCache;
                }

                return null;
            }
        }

        public DirectoryInfo OutputDirectory {
            get {
                if (this.OutputDirectyCache != null) {
                    return this.OutputDirectyCache;
                } else if (!string.IsNullOrWhiteSpace(this.OutputPath)) {
                    this.OutputDirectyCache = new DirectoryInfo(this.OutputPath);
                    return this.OutputDirectyCache;
                }

                return null;
            }
        }

        private DirectoryInfo InputDirectoryCache { get; set; }
        private DirectoryInfo OutputDirectyCache { get; set; }


        public bool IsValid {
            get {
                if (!this.InputDirectory.Exists) {
                    return false;
                }

                return true;
            }
        }
    }
}