using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EspionSpotify.Models;
using EspionSpotify.AudioSessions;
using EspionSpotify.Enums;
using EspionSpotify.Extensions;
using NAudio.CoreAudioApi;
using NAudio.Utils;
using NAudio.Wave;

namespace EspionSpotify.AudioSessions
{
    public sealed class AudioThrottler: IAudioThrottler, IDisposable
    {
        private bool _disposed;
        private const int DETECTED_SILENCE_MS = 500;
        private readonly object _lockObject;
        
        private const int BUFFER_TOTAL_SIZE_IN_SECOND = 4;
        
        private readonly IMainAudioSession _audioSession;

        private CancellationTokenSource _cancellationTokenSource;
        private WasapiLoopbackCapture _waveIn;
        private IAudioCircularBuffer _buffer;

        private int WaveAverageShortPerSecond => (int) (_waveIn.WaveFormat.AverageBytesPerSecond / 2.0);
        private int BufferReadOffset => (int)(_waveIn.WaveFormat.AverageBytesPerSecond * (BUFFER_TOTAL_SIZE_IN_SECOND / 4.0));
        private int BufferMaxLength => _waveIn.WaveFormat.AverageBytesPerSecond * BUFFER_TOTAL_SIZE_IN_SECOND;
        private int SilenceAverageShortLength => (int)(DETECTED_SILENCE_MS / 1_000.0 * WaveAverageShortPerSecond);
        
        public bool Running { get; set; }
        public WaveFormat WaveFormat => _waveIn.WaveFormat;

        public bool BufferIsHalfFull
        {
            get
            {
                lock (_lockObject)
                {
                    return _buffer.Count > (int)(BufferMaxLength / 2.0);
                }
            }
        }

        public bool BufferIsReady
        {
            get
            {
                lock (_lockObject)
                {
                    return _buffer.Count > BufferReadOffset;
                }
            }
        }

        public AudioThrottler(IMainAudioSession audioSession)
        {
            _audioSession = audioSession;
            _lockObject = new object();
        }
        
        public async Task Run(CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;

            if (_audioSession.AudioMMDevicesManager.AudioEndPointDevice == null) return;

            Running = true;

            _waveIn = new WasapiLoopbackCapture(_audioSession.AudioMMDevicesManager.AudioEndPointDevice);
            _waveIn.ShareMode = AudioClientShareMode.Shared;
            _waveIn.DataAvailable += WaveIn_DataAvailable;

            var silencer = CreateSilencer();
            silencer.Play();

            await Task.Delay(50);
            _waveIn.StartRecording();

            while (Running)
            {
                if (_cancellationTokenSource.IsCancellationRequested) return;
                await Task.Delay(100);
            }

            silencer.Stop();
            silencer.Dispose();
            
            _waveIn.StopRecording();
        }

        private WaveOutEvent CreateSilencer()
        {

            var silenceProvider = new SilenceProvider(_waveIn.WaveFormat).ToSampleProvider();
            var wo = new WaveOutEvent();
            wo.Init(silenceProvider);
            return wo;
        }

        public async Task<AudioWaveBuffer> Read(SilenceAnalyzer silence = SilenceAnalyzer.None)
        {
            AudioWaveBuffer result = null;

            await WaitBufferReady();
            
            switch (silence)
            {
                case SilenceAnalyzer.TrimStart:
                {
                    lock (_lockObject)
                    {
                        // temporary push
                        _buffer.Advance(_buffer.Count);

                        // var readPeek = _buffer.Peek(out var dataPeek, 0, _buffer.Count);
                        // var readPosition = BufferPositionWithoutSilence(dataPeek, readPeek, recursive: true);
                        // if (readPosition > 0)
                        // {
                        //     _buffer.Advance(readPosition);
                        // }
                        var read = _buffer.Read(out var data, 0, _waveIn.WaveFormat.AverageBytesPerSecond);
                        if (read > 0)
                        {
                            result = ToAudioWaveBuffer(data, read);
                        }
                    }

                    break;
                }
                case SilenceAnalyzer.TrimEnd:
                {
                    lock (_lockObject)
                    {
                        // var readPeek = _buffer.Peek(out var dataPeek, 0, _buffer.Count);
                        // var validRead = BufferPositionWithoutSilence(dataPeek, readPeek, recursive: false);
                        // var read = _buffer.Read(out var data, 0, validRead);
                        var read = _buffer.Read(out var data, 0, _buffer.Count);
                        if (read > 0)
                        {
                            result = ToAudioWaveBuffer(data, read);
                        }
                    }

                    break;
                }
                case SilenceAnalyzer.None:
                {
                    lock (_lockObject)
                    {
                        if (_buffer.Count >= BufferReadOffset)
                        {
                            var read = _buffer.Read(out var data, 0,
                            _waveIn.WaveFormat.AverageBytesPerSecond);
                            if (read > 0)
                            {
                                result = ToAudioWaveBuffer(data, read);
                            }
                        }
                    }

                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(silence), silence,
                        @"Cannot cast to Silence Analyzer. Not supported.");
                }
            }

            return result;
        }

        public async Task WaitBufferReady()
        {
            var timeout = BUFFER_TOTAL_SIZE_IN_SECOND;
            var pace = 100;
            while (!BufferIsHalfFull && timeout > 0)
            {
                timeout -= pace;
                await Task.Delay(pace);
            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (!Running) return;

            lock (_lockObject)
            {
                if (_buffer == null)
                {
                    _buffer = new AudioCircularBuffer(BufferMaxLength);
                }

                _buffer.Write(e.Buffer, 0, e.BytesRecorded);
            }
        }

        private AudioWaveBuffer ToAudioWaveBuffer(byte[] data, int read)
        {
            return new AudioWaveBuffer
            {
                Buffer = data,
                BytesRecordedCount = read,
                // WithSilence = WithSilence(data, read)
            };
        }
        
        // private bool WithSilence(byte[] buffer, int count)
        // {
        //     var received = new byte[count];
        //     Array.Copy(buffer, 0, received, 0, count);
        //     return received.TakeWhile(x => x == 0).Count() > SilenceAverageByteLength;
        // }

        private int BufferPositionWithoutSilence(byte[] buffer, int count, bool recursive = false)
        {
            var received = new byte[count];
            Array.Copy(buffer, 0, received, 0, count);
            var atPositionList = new List<int>();
            
            var bouncePerSample = (int) (SilenceAverageShortLength / 2.0);
            for (var i = 0; i < received.Length; i += bouncePerSample)
            {
                var sample = new short[SilenceAverageShortLength];
                var readCount = Math.Min(SilenceAverageShortLength, received.Length - i);
                Array.Copy(received, i, sample, 0, readCount);

                var average = sample.Average(x => x);
                if (average > 0) continue;

                atPositionList.Add(i);
                if (!recursive) break;
            }

            if (atPositionList.Any())
            {
                return recursive ? atPositionList.Last() : atPositionList.First();
            }

            return BufferReadOffset;
        }

        #region Dispose
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                _waveIn.Dispose();
            }

            _disposed = true;
        }

        #endregion DisposeRecorder
    }
}