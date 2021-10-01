using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFmpeg.AutoGen;

namespace FFmpegFirstTry
{
    public static class Remuxing
    {
        // one format/container -> another format/container
        public static unsafe void Start()
        {
            const string inputFileName = "Resources/SampleVideo_1280x720_2mb.mp4";
            const string outputFileName = "Output/Remuxing/remuxed.ts";
            Directory.CreateDirectory(Path.GetDirectoryName(outputFileName));

            AVFormatContext* inputFormatContext = null;
            AVFormatContext* outputFormatContext = null;

            // get format context and stream info
            if (ffmpeg.avformat_open_input(&inputFormatContext, inputFileName, null, null) < 0)
            {
                Console.WriteLine($"Could not open input file {inputFileName}");
                goto end;
            }
            if (ffmpeg.avformat_find_stream_info(inputFormatContext, null) < 0)
            {
                Console.WriteLine("Could not retrieve input stream information");
                goto end;
            }

            // allocate room for output format context
            ffmpeg.avformat_alloc_output_context2(&outputFormatContext, null, null, outputFileName);
            if (outputFormatContext == null)
            {
                Console.WriteLine("Could not create output context");
                goto end;
            }

            #region allocate output streams
            uint numStreams = inputFormatContext->nb_streams;
            // AVStream** streamList = (AVStream**)ffmpeg.av_malloc_array(numStreams, (ulong)sizeof(AVStream*));
            // List<IntPtr> streamList = new();
            // AVStream*[] streams = new AVStream*[numStreams];
            // input stream index -> output stream index
            int* streamIndexList = (int*)ffmpeg.av_malloc_array(numStreams, (ulong)sizeof(int));

            // loop through all the input streams
            int outputStreamIndex = 0;
            for (int i = 0; i < numStreams; i++)
            {
                AVStream* outStream;
                AVStream* inStream = inputFormatContext->streams[i];
                AVCodecParameters* inCodecParameters = inStream->codecpar;

                // skip not interested streams
                if (inCodecParameters->codec_type != AVMediaType.AVMEDIA_TYPE_AUDIO &&
                    inCodecParameters->codec_type != AVMediaType.AVMEDIA_TYPE_VIDEO &&
                    inCodecParameters->codec_type != AVMediaType.AVMEDIA_TYPE_SUBTITLE)
                {
                    streamIndexList[i] = -1;
                    continue;
                }

                // get mapping from input stream index to output stream index
                streamIndexList[i] = outputStreamIndex++;

                // make room for a new stream in output format
                outStream = ffmpeg.avformat_new_stream(outputFormatContext, null);
                if (outStream == null)
                {
                    Console.WriteLine("Could not create output stream");
                    goto end;
                }

                // copy parameters from input stream to output stream
                if (ffmpeg.avcodec_parameters_copy(outStream->codecpar, inCodecParameters) < 0)
                {
                    Console.WriteLine("Could not copy codec parameters");
                    goto end;
                }
            }
            #endregion

            // create the output file
            if ((outputFormatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
            {
                // the output format is not using other I/O methods than writing a file.

                // open output file
                if (ffmpeg.avio_open(&outputFormatContext->pb, outputFileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
                {
                    Console.WriteLine($"Could not open output file {outputFileName}");
                    goto end;
                }
            }

            // write header to output file
            if (ffmpeg.avformat_write_header(outputFormatContext, null) < 0)
            {
                Console.WriteLine("Could not write header to output file");
                goto end;
            }

            // copy streams, packet by packet, from input to output
            AVPacket* packet = ffmpeg.av_packet_alloc();
            while (!(ffmpeg.av_read_frame(inputFormatContext, packet) < 0))
            {
                AVStream* outStream;
                AVStream* inStream = inputFormatContext->streams[packet->stream_index];
                if (packet->stream_index >= numStreams ||
                    streamIndexList[packet->stream_index] < 0)
                {
                    // wipe the packet
                    ffmpeg.av_packet_unref(packet);
                    continue;
                }

                // set output stream index
                packet->stream_index = streamIndexList[packet->stream_index];

                // get output stream
                outStream = outputFormatContext->streams[packet->stream_index];

                // copy packet
                packet->pts = ffmpeg.av_rescale_q_rnd(packet->pts, inStream->time_base, outStream->time_base, AVRounding.AV_ROUND_INF | AVRounding.AV_ROUND_PASS_MINMAX);
                packet->dts = ffmpeg.av_rescale_q_rnd(packet->dts, inStream->time_base, outStream->time_base, AVRounding.AV_ROUND_INF | AVRounding.AV_ROUND_PASS_MINMAX);
                packet->duration = ffmpeg.av_rescale_q(packet->duration, inStream->time_base, outStream->time_base);

                packet->pos = -1;

                // write a frame to an output stream
                if (ffmpeg.av_interleaved_write_frame(outputFormatContext, packet) < 0)
                {
                    Console.WriteLine("Could not write a frame to an output stream");
                    break;
                }

                // dispose packet
                ffmpeg.av_packet_unref(packet);
            }
            ffmpeg.av_packet_free(&packet);

            // write a stream trailer to the output format
            ffmpeg.av_write_trailer(outputFormatContext);

        end:
            ffmpeg.avformat_free_context(inputFormatContext);
            ffmpeg.avformat_free_context(outputFormatContext);

            Console.WriteLine("end");
        }
    }
}
