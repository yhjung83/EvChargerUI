using System;
using System.IO;
using System.Media;
using System.Windows;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services
{
    public class SoundService : ISoundService
    {
        private static readonly Lazy<SoundService> _instance = new Lazy<SoundService>(() => new SoundService());
        
        public static SoundService Instance => _instance.Value;

        private readonly FileLogger _logger = ((App)Application.Current).AppLogger;
        private readonly string _soundDirectory;
        private SoundPlayer _currentPlayer;

        private SoundService()
        {
            // 실행 파일 기준으로 Sound 폴더 경로 찾기
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _soundDirectory = Path.Combine(baseDirectory, "Sound");

            // Sound 폴더가 없으면 로그 남기기
            if (!Directory.Exists(_soundDirectory))
            {
                _logger.Warn($"[SoundService] Sound directory not found: {_soundDirectory}");
            }
        }

        public void PlaySoundAsync(string fileName)
        {
            try
            {
                string filePath = GetSoundFilePath(fileName);
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                // 기존 재생 중인 소리 중지
                StopSound();

                _currentPlayer = new SoundPlayer(filePath);
                _currentPlayer.Play();
                _logger.Debug($"[SoundService] 비동기 재생 시작: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[SoundService] 소리 재생 실패 ({fileName}): {ex.Message}");
            }
        }

        public void StopSound()
        {
            try
            {
                if (_currentPlayer != null)
                {
                    _currentPlayer.Stop();
                    _currentPlayer.Dispose();
                    _currentPlayer = null;
                    _logger.Debug("[SoundService] 소리 재생 중지");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[SoundService] 소리 중지 실패: {ex.Message}");
            }
        }

        private string GetSoundFilePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                _logger.Warn("[SoundService] 파일명이 비어있습니다.");
                return null;
            }

            // .wav 확장자가 없으면 추가
            if (!fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".wav";
            }

            string filePath = Path.Combine(_soundDirectory, fileName);

            if (!File.Exists(filePath))
            {
                _logger.Warn($"[SoundService] 소리 파일을 찾을 수 없습니다: {filePath}");
                return null;
            }

            return filePath;
        }
    }
}

