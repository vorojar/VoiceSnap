using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace VoiceSnap
{
    /// <summary>
    /// 音频录制服务 - 纯内存操作，绝不产生任何临时文件
    /// </summary>
    public class AudioRecorder : IDisposable
    {
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioDataStream;
        private bool _isRecording = false;
        
        // 录音参数
        private const int SampleRate = 16000;
        private const int Channels = 1;
        private const int BitsPerSample = 16;
        
        // 当前音量 (0-1)
        public float CurrentVolume { get; private set; }
        
        // 录音状态
        public bool IsRecording => _isRecording;
        
        // 音量更新事件
        public event Action<float>? VolumeUpdated;

        public string GetDeviceName()
        {
            try
            {
                if (WaveInEvent.DeviceCount > 0)
                {
                    var capabilities = WaveInEvent.GetCapabilities(0);
                    return capabilities.ProductName;
                }
            }
            catch { }
            return "默认录音设备";
        }
        
        /// <summary>
        /// 开始录音
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording) return;
            
            try
            {
                App.Log("AudioRecorder: 纯内存录音开始");
                
                _audioDataStream = new MemoryStream();
                
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                    BufferMilliseconds = 50
                };
                
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                
                _waveIn.StartRecording();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                App.LogError("AudioRecorder: 启动录音失败", ex);
                Cleanup();
            }
        }
        
        /// <summary>
        /// 停止录音并返回带 WAV 头的音频数据
        /// </summary>
        public byte[]? StopRecording()
        {
            if (!_isRecording) return null;
            
            _isRecording = false;
            
            try
            {
                _waveIn?.StopRecording();
                
                byte[]? finalData = null;
                if (_audioDataStream != null && _audioDataStream.Length > 0)
                {
                    // 手动构建 WAV 文件头 (44 字节)
                    byte[] rawData = _audioDataStream.ToArray();
                    finalData = BuildWavWithHeader(rawData);
                    App.Log($"AudioRecorder: 录音完成, 原始大小: {rawData.Length}, 总大小: {finalData.Length}");
                }
                
                Cleanup();
                return finalData;
            }
            catch (Exception ex)
            {
                App.LogError("AudioRecorder: 停止录音失败", ex);
                Cleanup();
                return null;
            }
        }
        
        private byte[] BuildWavWithHeader(byte[] pcmData)
        {
            int headerSize = 44;
            int totalSize = headerSize + pcmData.Length;
            byte[] wavData = new byte[totalSize];
            
            using (var ms = new MemoryStream(wavData))
            using (var writer = new BinaryWriter(ms))
            {
                // RIFF header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(totalSize - 8);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                
                // fmt chunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // chunk size
                writer.Write((short)1); // PCM format
                writer.Write((short)Channels);
                writer.Write(SampleRate);
                writer.Write(SampleRate * Channels * (BitsPerSample / 8)); // byte rate
                writer.Write((short)(Channels * (BitsPerSample / 8))); // block align
                writer.Write((short)BitsPerSample);
                
                // data chunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(pcmData.Length);
                writer.Write(pcmData);
            }
            
            return wavData;
        }
        
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isRecording) return;
            
            try
            {
                // 直接写入原始 PCM 数据
                _audioDataStream?.Write(e.Buffer, 0, e.BytesRecorded);
                
                // 计算音量 (RMS)
                float sum = 0;
                int sampleCount = e.BytesRecorded / 2;
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                    float normalized = sample / 32768f;
                    sum += normalized * normalized;
                }
                float rms = (float)Math.Sqrt(sum / sampleCount);
                CurrentVolume = Math.Min(1.0f, rms * 8);
                VolumeUpdated?.Invoke(CurrentVolume);
            }
            catch { }
        }
        
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null) App.LogError("AudioRecorder: 异常停止", e.Exception);
        }
        
        private void Cleanup()
        {
            _waveIn?.Dispose();
            _waveIn = null;
            _audioDataStream?.Dispose();
            _audioDataStream = null;
        }
        
        public void Dispose()
        {
            if (_isRecording) StopRecording();
            Cleanup();
        }
    }
}
