using Xabe.FFmpeg;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xabe.FFmpeg.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.IO;
using Cake.FFMpegRunner.Models;
using Xabe.FFmpeg.Streams.SubtitleStream;

namespace Cake.FFMpegRunner {
    /// <summary>
    /// A class for running FFMPeg and transcoding a list of files
    /// </summary>
    internal sealed class FFMpegRunner {
        #region Private readonly variables
        private readonly Models.Configuration _config;
        private readonly ILogger<FFMpegRunner> _logger;
        #endregion

        #region Private properties
        ///<summary>
        /// The current conversion that is being processed by this instance
        /// </summary>
        private Models.Conversion CurrentConversion { get; set; }
        #endregion

        #region Public constructor
        public FFMpegRunner(Models.Configuration configuration, ILogger<FFMpegRunner> logger) {
            this._config = configuration;
            _logger = logger;
        }
        #endregion

        #region Public methods
        ///<summary>
        /// Creates a list of converters which can be processed by the RunConverters method using the default file search method
        ///</summary>
        private IAsyncEnumerable<IConversion> BuildConverters() {
            var files = _config.InputDirectory.GetFiles(_config.InputSearchPattern,
                System.IO.SearchOption.AllDirectories);
            return this.BuildConverters(files.Select(o => new Models.ConversionRequest() {
                InputFile = o,
                OutputLocatation = _config.OutputDirectory,
                Type = this._config.ConversionType,
            }));
        }

        ///<summary>
        /// Creates a list of converters which can be processed by the RunConverters method using the supplied transcode requests
        ///</summary>
        private async IAsyncEnumerable<IConversion> BuildConverters(IEnumerable<Models.ConversionRequest> conversionRequests) {
            var requests = conversionRequests.ToArray();
            _logger.LogInformation($"Found {{0}} files to transcode using the pattern {{1}}",
                requests.Length,
                _config.InputSearchPattern);

            foreach (var request in requests) {
                if (request.InputFile.Exists) {
                    var mediainfo = await FFmpeg.GetMediaInfo(request.InputFile.FullName);

                    //Console.WriteLine(_config.InputDirectory.FullName);
                    var outputlocation = new DirectoryInfo(Path.Combine(request.OutputLocatation.FullName,
                        Path.GetRelativePath(_config.InputDirectory.FullName, request.InputFile.Directory.FullName)));
                    if (!outputlocation.Exists && !_config.SimulateMode) {
                        outputlocation.Create();
                    }
                    //  Console.WriteLine(outputlocation);

                    var converter = FFmpeg.Conversions.New()
                        .AddStream(mediainfo.VideoStreams)
                        .AddStream(mediainfo.AudioStreams)
                        //      .AddStream(mediainfo.SubtitleStreams)
                        .UseMultiThread(0)
                        .SetOutput(Path.Combine(outputlocation.FullName,
                            Path.ChangeExtension(request.InputFile.Name, ".mkv"))); //.SetOverwriteOutput(true)
                    //While the wrapper library supports hardware acceleration, it deos not seem to actually work and there is no documentation about how to use it. So we can just manually specify all the actual required arguments
                    //.AddParameter("-vaapi_device /dev/dri/renderD128", ParameterPosition.PreInput)
                    // .AddParameter("-vf format=nv12,hwupload", ParameterPosition.PostInput)
                    //.AddParameter("-c:v hevc_vaapi", ParameterPosition.PostInput)

                    if (request.Type == ConversionRequest.ConversionType.Video) {
                        long? bitrate = mediainfo.VideoStreams.FirstOrDefault()?.Bitrate;
                        var videostream = mediainfo.VideoStreams.FirstOrDefault();

                        //We can do a rough estimate for the required bitrate based on the video width
                        bitrate = videostream.Width switch {
                            >= 1280 and <= 4000 => 2200000,
                            <= 1280 => 400000,
                            _ => 1000
                        };

                        converter.AddParameter("-c:v hevc", ParameterPosition.PostInput);
                        converter.SetVideoBitrate(bitrate.Value);
                    } else {
                        converter.AddParameter("-c:v copy", ParameterPosition.PostInput);
                        converter.AddParameter("-c:a copy", ParameterPosition.PostInput);
                        converter = await this.FixSubtitlesAsync(converter, request.InputFile);
                    }

                    yield return converter;
                }
            }
        }

        ///<summary>
        /// Runs all the currently configured converters
        /// </summary>
        public async Task RunConverters() {
            var converters = await this.BuildConverters().ToListAsync();
            if (!converters.Any()) {
                _logger.LogInformation("No converters were supplied, there is nothing to process");
                return;
            }

            var totalconverters = converters.Count;
            uint count = 1;
            uint skipped = 0;
            uint failed = 0;

            var batchStartTime = DateTime.Now;
            foreach (var converter in converters) {
                //Put in a try to continue onto the next transcode if this one fails
                try {
                    this.CurrentConversion = new Models.Conversion() {
                        Destination = converter.OutputFilePath,
                        FileName = System.IO.Path.GetFileName(converter.OutputFilePath),
                        Progress = 0,
                        FFMpegArguments = converter.Build(),
                    };

                    var outputFile = new FileInfo(Path.ChangeExtension(this.CurrentConversion.Destination, "mkv"));
                    //Ignore empty files
                    if (outputFile.Exists && outputFile.Length > 0) {
                        _logger.LogInformation("File {0} already exists in {1}, skipping",
                            this.CurrentConversion.FileName,
                            this.CurrentConversion.Destination);
                        ++skipped;
                        continue;
                    } else if (outputFile.Exists && outputFile.Length == 0) {
                        _logger.LogInformation("File {0} already exists in {1}, however it contained no data. Overwriting",
                            this.CurrentConversion.FileName,
                            this.CurrentConversion.Destination);
                        //Empty files can be overwritten so we can retry them,
                        //however to do this we need to rebuild the arguments to allow them to be overwritten
                        converter.SetOverwriteOutput(true);
                        converter.SetOutput(Path.ChangeExtension(converter.OutputFilePath, "mp4"));
                        this.CurrentConversion = this.CurrentConversion with {
                            FFMpegArguments = converter.Build()
                        };
                    }

                    var startTime = DateTime.Now;

                    _logger.LogInformation(
                        $"[{{0}}/{{1}}]{Environment.NewLine}Attempting to run the following converter: {{2}} with the following arguments:{Environment.NewLine}{Environment.NewLine}{{3}}{Environment.NewLine}",
                        count,
                        totalconverters,
                        this.CurrentConversion.FileName,
                        this.CurrentConversion.FFMpegArguments);
                    converter.OnProgress += OnConverterProgress;

                    if (!_config.SimulateMode) {
                        _ = await converter.Start();
                    }

                    converter.OnProgress -= OnConverterProgress;

                    _logger.LogInformation("Finished in {0}", (DateTime.Now - startTime).TotalSeconds);
                } catch (Exception ex) {
                    _logger.LogError(ex,
                        "There was a problem processing the conversion {0} with the arguments {1}",
                        this.CurrentConversion.FileName,
                        this.CurrentConversion.FFMpegArguments);
                    failed++;
                } finally {
                    count++;
                    converter.OnProgress -= OnConverterProgress;
                }
            }

            this._logger.LogInformation("Finished all conversions in {0}", (DateTime.Now - batchStartTime).TotalSeconds);
            this._logger.LogInformation("Skipped {0} files, {1} files failed to be transcoded", skipped, failed);
        }
        #endregion

        #region Private methods
        private async Task<IConversion> FixSubtitlesAsync([NotNull] IConversion conversion, [NotNull] FileInfo inputFile) {
            var mediainfotask = FFmpeg.GetMediaInfo(inputFile.FullName);

            var codec = Xabe.FFmpeg.Streams.SubtitleStream.SubtitleCodec.srt;
            switch (Path.GetExtension(conversion.OutputFilePath)) {
                case "mp4":
                    codec = SubtitleCodec.mov_text;
                    break;
                case "mkv":
                    codec = SubtitleCodec.srt;
                    break;
            }

//HACK: For now just assume a single video stream. This will be accurate in 99.999999% of all cases
            var mediainfo = await mediainfotask;
            var videostream = mediainfo.VideoStreams.First();
            foreach (var subtitle in mediainfo.SubtitleStreams) {
                subtitle.SetCodec(codec);
                conversion.AddStream(subtitle);
                //Need to fix DVB subtitles which some video players can't handle properly
                if (subtitle.Codec == "dvb_teletext") {
                    conversion
                        .AddParameter("-fix_sub_duration", ParameterPosition.PreInput)
                        //Make sure we fix the framerate for subtitles
                        .AddParameter($"-filter:s fps={videostream.Framerate}", ParameterPosition.PostInput)
                        .AddParameter("-txt_format text", ParameterPosition.PreInput);
                    // break;
                    //}
                }
            }

            return conversion;
        }
        #endregion

        #region Event handler
        private void OnConverterProgress(object sender, ConversionProgressEventArgs e) {
            if (this.CurrentConversion != null && this.CurrentConversion.Progress != e.Percent) {
                this.CurrentConversion.Progress = e.Percent;

                _logger.LogInformation("Current Progress: {0}% for {1}", this.CurrentConversion.Progress, this.CurrentConversion.FileName);
            }
        }
        #endregion
    }
}
