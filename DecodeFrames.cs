using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using System.Text;

namespace FFmpegFirstTry
{
    public static class DecodeFrames
    {
        public static unsafe void Start()
        {
            const string fileName = "Resources/SampleVideo_1280x720_2mb.mp4";
            const string frameFileBaseName = "Output/Frames/frame";

            string directoryPath =Path.GetDirectoryName(frameFileBaseName);
            Directory.CreateDirectory(directoryPath);
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                File.Delete(file);
            }

            // get format/container metadata
            AVFormatContext* formatContext = ffmpeg.avformat_alloc_context();
            ffmpeg.avformat_open_input(&formatContext, fileName, null, null);
            string formatName =
                Marshal.PtrToStringAnsi((IntPtr)formatContext->iformat->long_name);

            Console.WriteLine($"Format {formatName}, duration {formatContext->duration} us");

            // get stream
            ffmpeg.avformat_find_stream_info(formatContext, null);
            for (int i = 0; i < formatContext->nb_streams; i++)
            {
                // find decoder
                AVCodecParameters* localCodecParameters =
                    formatContext->streams[i]->codecpar;
                AVCodec* localCodec = ffmpeg.avcodec_find_decoder(
                    localCodecParameters->codec_id
                );

                // print information
                StringBuilder info = new();
                if (localCodecParameters->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    info.Append($"Video Codec: resolution {localCodecParameters->width} x {localCodecParameters->height}");
                }
                else if (localCodecParameters->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    info.Append($"Audio Codec: {localCodecParameters->channels} channels, sample rate {localCodecParameters->sample_rate}");
                }
                info.Append("\t");
                string codecName = Marshal.PtrToStringAnsi((IntPtr)localCodec->long_name);
                info.Append($"Codec {codecName}, ID {localCodec->id}, bit_rate {localCodecParameters->bit_rate}");
                Console.WriteLine(info);

                // load the codec
                AVCodecContext* codecContext = ffmpeg.avcodec_alloc_context3(localCodec);
                ffmpeg.avcodec_parameters_to_context(codecContext, localCodecParameters);
                ffmpeg.avcodec_open2(codecContext, localCodec, null);

                // read packets from stream
                AVPacket* packet = ffmpeg.av_packet_alloc();
                AVFrame* frame = ffmpeg.av_frame_alloc();
                for (int frameCount = 0; ffmpeg.av_read_frame(formatContext, packet) >= 0; frameCount++)
                {
                    ffmpeg.avcodec_send_packet(codecContext, packet);
                    ffmpeg.avcodec_receive_frame(codecContext, frame);

                    // print information
                    char pictureTypeChar = (char)ffmpeg.av_get_picture_type_char(frame->pict_type);
                    Console.WriteLine(
                        $"Frame {pictureTypeChar} ({codecContext->frame_number}), pts {frame->pts}, dts {frame->pkt_dts}, key_frame {frame->key_frame}, [coded_picture_number {frame->coded_picture_number}, display_picture_number {frame->display_picture_number}]"
                    );

                    // save the frame
                    SaveGrayFrame(frame->data[0], frame->linesize[0], frame->width, frame->height, $"{frameFileBaseName}.{frameCount}.{codecContext->frame_number}.pgm");
                }

                // dispose the objects
                ffmpeg.avcodec_free_context(&codecContext);
                ffmpeg.av_packet_free(&packet);
                ffmpeg.av_frame_free(&frame);
            }

            // dispose the objects
            ffmpeg.avformat_free_context(formatContext);
        }

        // private static void SaveGrayFrame(byte[] buffer, int wrap, int xSize, int ySize, string fileName)
        private static unsafe void SaveGrayFrame(byte* buffer, int wrap, int xSize, int ySize, string fileName)
        {
            using var fileStream = File.OpenWrite(fileName);
            using var binaryWriter = new BinaryWriter(fileStream);
            byte[] header = Encoding.ASCII.GetBytes($"P5\n{xSize} {ySize}\n{255}\n");
            binaryWriter.Write(header);

            // write line by line
            for (int i = 0; i < ySize; i++)
            {
                // fileStream.Write(buffer, 0, xSize);
                for (int j = 0; j < xSize; j++)
                {
                    IntPtr currentBufferPointer = (IntPtr)buffer + j + i * wrap;
                    byte readByte = Marshal.ReadByte(currentBufferPointer);
                    binaryWriter.Write(readByte);
                }
            }
        }
    }
}
