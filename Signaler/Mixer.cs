using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SIPSorcery.Net;
using System.Collections.Concurrent;

namespace Signaler
{
    public class Mixer
    {
        private readonly ILogger<Mixer> _logger;

        private uint _timestamp;
        private Stream _audioBuffer;
        private ConcurrentQueue<RTPPacket> _queue;

        public Mixer(ILogger<Mixer> logger)
        {
            _logger = logger;
            _audioBuffer = new MemoryStream();
            _queue = new ConcurrentQueue<RTPPacket>();
            _timestamp = 1234587;
        }

        /// <summary>
        ///     Adiciona um pacote ao buffer
        /// </summary>
        public void AddRawPacket(byte[] pkt)
        {
            _audioBuffer.Write(pkt, 0, pkt.Length);
        }

        public void AddRawPacket(RTPPacket pk)
        {
            _queue.Enqueue(pk);
        }

        public void StartAudioProcess()
        {
            Task.Run(ProcessAudio);
            Task.Run(ProcessRTPPacket);
        }

        private void ProcessRTPPacket()
        {
            while (true)
            {
                Task.Delay(10);

                if (!_queue.IsEmpty)
                {
                    try
                    {
                        using var tmpStream = new MemoryStream();
                        while (_queue.TryDequeue(out var pkt))
                            tmpStream.Write(pkt.Payload, 0, pkt.Payload.Length);
                        var waveFormat = WaveFormat.CreateMuLawFormat(8000, 1);
                        var reader = new RawSourceWaveStream(tmpStream, waveFormat);
                        using var convertedStream = WaveFormatConversionStream.CreatePcmStream(reader);
                        var bytes = new byte[convertedStream.Length];
                        convertedStream.Read(bytes, 0, (int)convertedStream.Length);
                        HasAudioData.Invoke(this, new TesteEventArgs { bytes = bytes, Timestamp = _timestamp++ });
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
        }

        public EventHandler<TesteEventArgs> HasAudioData;

        public class TesteEventArgs : EventArgs
        {
            public byte[] bytes { get; set; }
            public uint Timestamp { get; set; }
        }


        private void ProcessAudio()
        {
            while (true)
            {
                if (_audioBuffer.Length > 0)
                {
                    var waveFormat = WaveFormat.CreateMuLawFormat(8000, 1);
                    using var tmpMemStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());
                    var reader = new RawSourceWaveStream(tmpMemStream, waveFormat);
                    using var convertedStream = WaveFormatConversionStream.CreatePcmStream(reader);
                    WaveFileWriter.CreateWaveFile(@"C:\\temp\\wavs\\teste5.wav", convertedStream);
                }
            }
        }
    }

    public class RawSourceWaveStream : WaveStream
    {
        private Stream sourceStream;
        private WaveFormat waveFormat;

        public RawSourceWaveStream(Stream sourceStream, WaveFormat waveFormat)
        {
            this.sourceStream = sourceStream;
            this.waveFormat = waveFormat;
        }

        public override WaveFormat WaveFormat
        {
            get { return this.waveFormat; }
        }

        public override long Length
        {
            get { return this.sourceStream.Length; }
        }

        public override long Position
        {
            get
            {
                return this.sourceStream.Position;
            }
            set
            {
                this.sourceStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return sourceStream.Read(buffer, offset, count);
        }
    }
}
