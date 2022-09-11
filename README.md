# FFMpegRunner

A small little utility to hardware accelerate transcode a large group files to HEVC using FFMPEG. Designed to be used on Linux only. The code is in a fairly rough shape since it was primarily made to be used once for my personal use and by someone with access to the source code.

# Options
| Option  | Description   | 
|-------------- | -------------- 
| Input    | The input directory to scan. This will search all sub folders for files      |
| Output | The output directory to save the transcoded files |
| Pattern | The search pattern for input files. Typically used with a wild card, EG: `*.mkv` |
| Simulate | Simulates running the transcode without actually running FFMpeg. Useful for testing FFMpeg arguments and directory searching |
