using FFmpeg.AutoGen;

namespace FFmpegFirstTry
{
    public static class Sampling
    {
        public static void Start()
        {
            const string audioFilePath = "Resources/file_example_MP3_1MG.mp3";
            const string outputFigurePath = "Output/Sampling/waveform.png";
            // const int sampleRate = 44100;
            const int sampleRate = 300;
            List<double> sampleData = DecodeAudioFile(audioFilePath, sampleRate);

            var plt = new ScottPlot.Plot(400, 300);
            double[] xs = new double[sampleData.Count];
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] = i;
            }
            plt.AddSignalXY(xs, sampleData.ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(outputFigurePath));
            plt.SaveFig(outputFigurePath);
        }

        // public static unsafe (double*, int) DecodeAudioFile(string audioFile, int sampleRate)
        public static unsafe List<double> DecodeAudioFile(string audioFile, int sampleRate)
        {
            // initialize all muxers, demuxers and protocols for libavformat
            ffmpeg.av_register_all();

            // get format and stream info from audio file
            AVFormatContext* format = ffmpeg.avformat_alloc_context();
            if (ffmpeg.avformat_open_input(&format, audioFile, null, null) < 0)
            {
                ffmpeg.avformat_free_context(format);
                throw new Exception($"Could not open input file {audioFile}.");
            }
            if (ffmpeg.avformat_find_stream_info(format, null) < 0)
            {
                ffmpeg.avformat_free_context(format);
                throw new Exception("Could not retrieve input stream information.");
            }

            #region get the the first audio stream
            // get the index of the first audio stream
            int streamIndex = -1;
            for (int i = 0; i < format->nb_streams; i++)
            {
                if (format->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    streamIndex = i;
                    break;
                }
            }
            if (streamIndex == -1)
            {
                ffmpeg.avformat_free_context(format);
                throw new Exception("There is no audio stream.");
            }
            // get the stream
            AVStream* stream = format->streams[streamIndex];
            #endregion

            // get codec
            AVCodecContext* codec = stream->codec;
            if (ffmpeg.avcodec_open2(codec, ffmpeg.avcodec_find_decoder(codec->codec_id), null) < 0)
            {
                ffmpeg.avformat_free_context(format);
                throw new Exception($"Could not open decoder for stream #{streamIndex}");
            }

            // get software resampler
            SwrContext* resampler = ffmpeg.swr_alloc();
            ffmpeg.av_opt_set_int(resampler, "in_channel_count", codec->channels, 0);
            ffmpeg.av_opt_set_int(resampler, "out_channel_count", 1, 0);
            ffmpeg.av_opt_set_int(resampler, "in_channel_layout", (long)codec->channel_layout, 0);
            ffmpeg.av_opt_set_int(resampler, "out_channel_layout", ffmpeg.AV_CH_LAYOUT_MONO, 0);
            ffmpeg.av_opt_set_int(resampler, "in_sample_rate", codec->sample_rate, 0);
            ffmpeg.av_opt_set_int(resampler, "out_sample_rate", sampleRate, 0);
            // bit depth = sample format
            ffmpeg.av_opt_set_sample_fmt(resampler, "in_sample_fmt", codec->sample_fmt, 0);
            ffmpeg.av_opt_set_sample_fmt(resampler, "out_sample_fmt", AVSampleFormat.AV_SAMPLE_FMT_DBL, 0);
            ffmpeg.swr_init(resampler);
            if (ffmpeg.swr_is_initialized(resampler) == 0)
            {
                ffmpeg.swr_free(&resampler);
                ffmpeg.avformat_free_context(format);
                throw new Exception($"Could not initialize resampler");
            }

            // get tmp packet and frame to read data
            AVPacket* packet = ffmpeg.av_packet_alloc();
            AVFrame* frame = ffmpeg.av_frame_alloc();
            if (frame == null)
            {
                ffmpeg.av_packet_unref(packet);
                ffmpeg.swr_free(&resampler);
                ffmpeg.avformat_free_context(format);
                throw new Exception($"Could not allocate the temporary frame");
            }

            // iterate through frames
            // double* data = null;
            List<double> data = new();
            // int size = 0;
            while (ffmpeg.av_read_frame(format, packet) >= 0)
            {
                // decode one frame
                int gotFrame;
                if (ffmpeg.avcodec_decode_audio4(codec, frame, &gotFrame, packet) < 0)
                {
                    break;
                }
                if (gotFrame == 0)
                {
                    continue;
                }

                // resample frames
                double* buffer;
                ffmpeg.av_samples_alloc((byte**)&buffer, null, 1, frame->nb_samples, AVSampleFormat.AV_SAMPLE_FMT_DBL, 0);
                int frameCount;
                fixed (byte** frameData = frame->data.ToArray())
                {
                    frameCount = ffmpeg.swr_convert(resampler, (byte**)&buffer, frame->nb_samples, frameData, frame->nb_samples);
                }

                // append resampled frames to data
                for (int i = 0; i < frameCount; i++)
                {
                    double tmp = buffer[i];
                    data.Add(tmp);
                }
            }

            // clean up
            ffmpeg.av_frame_free(&frame);
            ffmpeg.av_packet_unref(packet);
            ffmpeg.swr_free(&resampler);
            ffmpeg.avformat_free_context(format);

            return data;
        }
    }
}
