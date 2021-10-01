using FFmpeg.AutoGen;
using FFmpegFirstTry;

Console.WriteLine("Hello, World!");

FFmpeg.AutoGen.Example.FFmpegBinariesHelper.RegisterFFmpegBinaries();

Console.WriteLine(ffmpeg.av_version_info());

DecodeFrames.Start();
Remuxing.Start();
Sampling.Start();
