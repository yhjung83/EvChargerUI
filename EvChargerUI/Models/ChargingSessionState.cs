using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EvChargerUI.Commons.Enum;
using EvChargerUI.Commons.Util;
using Newtonsoft.Json;

namespace EvChargerUI.Models
{
    /// <summary>
    /// 충전 세션 상태 정보
    /// </summary>
    public class ChargingSessionState
    {
        public int ConnectorId { get; set; } // 채널 번호
        public string StationId { get; set; }
        public string ChargerId { get; set; }
        public double StartEnergy { get; set; } // 충전 시작 시 전력량 (kWh)
        public double LastEnergy { get; set; } // 마지막 전력량 (kWh)
        public bool IsCardPaid { get; set; } // 카드 결제 여부
        public string CardNumber { get; set; } // 카드 번호 (MembershipNo 또는 결제 정보에서)
        public DateTime StartTime { get; set; } // 충전 시작 시각
        public string Status { get; set; } // "Charging", "Stopped", "Completed"
        public int ChargingSelect { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string MembershipNo { get; set; }
        public PaymentInfoBackup PrePaymentInfo { get; set; }
        public string QrTid { get; set; } // QR 결제 시 tid 저장
        public float CurrentUserUnitCost { get; set; }
        public string OrderNo { get; set; }
        public DateTime LastUpdateTime { get; set; } // 마지막 업데이트 시간
        public int UserSetChargeAmount { get; set; } // 사용자 설정 충전 금액
    }

    /// <summary>
    /// PaymentInfo 백업용 클래스
    /// </summary>
    public class PaymentInfoBackup
    {
        public string PayCode { get; set; }
        public string AuthNum { get; set; }
        public string TotalCost { get; set; }
        public string PayDate { get; set; }
        public string PayTime { get; set; }
        public string PgNum { get; set; }
    }

    /// <summary>
    /// 충전 세션 상태 관리 클래스
    /// - lock: 동일 프로세스 내 스레드 간 동시 접근 방지
    /// - 임시 파일(.tmp): 쓰기 완료 후 원자적 교체로 파일 손상 방지
    /// - 재시도: 외부 프로세스(안티바이러스 등) 파일 잠금 대응
    /// </summary>
    public static class ChargingSessionManager
    {
        private static readonly string SessionDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions");
        private static readonly string SessionFilePrefix = "session";
        private static readonly object _fileLock = new object();
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 100;

        static ChargingSessionManager()
        {
            if (!Directory.Exists(SessionDirectory))
            {
                Directory.CreateDirectory(SessionDirectory);
            }
        }

        /// <summary>
        /// 충전 세션 상태 저장 (lock + 임시파일 + 재시도)
        /// </summary>
        public static void SaveSession(ChargerChannel channel, double currentEnergy, string status)
        {
            lock (_fileLock)
            {
                // BasePowerMeter가 0이거나 설정되지 않은 경우, 이미 저장된 세션의 StartEnergy 유지
                double startEnergy = channel.BasePowerMeter;
                if (startEnergy <= 0)
                {
                    try
                    {
                        var existingSession = LoadSessionInternal(channel.ChannelNo);
                        if (existingSession != null && existingSession.StartEnergy > 0)
                        {
                            startEnergy = existingSession.StartEnergy;
                        }
                    }
                    catch
                    {
                        // 기존 세션 로드 실패 시 무시
                    }
                }

                var session = new ChargingSessionState
                {
                    ConnectorId = channel.ChannelNo,
                    StationId = channel.StationId,
                    ChargerId = channel.ChargerId,
                    StartEnergy = startEnergy,
                    LastEnergy = currentEnergy,
                    IsCardPaid = channel.PaymentMethod == PaymentMethod.IcCard ||
                                 channel.PaymentMethod == PaymentMethod.SamsungPay,
                    CardNumber = channel.MembershipNo ?? "-9999",
                    StartTime = channel.ChargingStartTime,
                    Status = status,
                    ChargingSelect = channel.ChargingSelect,
                    PaymentMethod = channel.PaymentMethod,
                    MembershipNo = channel.MembershipNo,
                    QrTid = channel.QrTid,
                    CurrentUserUnitCost = channel.CurrentUserUnitCost,
                    OrderNo = channel.OrderNo,
                    LastUpdateTime = DateTime.Now,
                    UserSetChargeAmount = channel.UserSetChargeAmount
                };

                if (channel.PrePaymentInfo != null)
                {
                    session.PrePaymentInfo = new PaymentInfoBackup
                    {
                        PayCode = channel.PrePaymentInfo.PayCode,
                        AuthNum = channel.PrePaymentInfo.AuthNum,
                        TotalCost = channel.PrePaymentInfo.TotalCost,
                        PayDate = channel.PrePaymentInfo.PayDate,
                        PayTime = channel.PrePaymentInfo.PayTime,
                        PgNum = channel.PrePaymentInfo.PgNum
                    };
                }

                string filePath = Path.Combine(SessionDirectory, $"{SessionFilePrefix}_{channel.ChannelNo}.json");
                string tempPath = Path.Combine(SessionDirectory, $"{SessionFilePrefix}_{channel.ChannelNo}.tmp");
                string json = JsonConvert.SerializeObject(session, Formatting.Indented);

                for (int retry = 0; retry < MaxRetries; retry++)
                {
                    try
                    {
                        // 1) 임시 파일에 독점 모드로 쓰기
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var writer = new StreamWriter(fs))
                        {
                            writer.Write(json);
                            writer.Flush();
                            fs.Flush(true);
                        }

                        // 2) 기존 파일 삭제 후 임시 파일을 원본으로 교체
                        if (File.Exists(filePath))
                            File.Delete(filePath);

                        File.Move(tempPath, filePath);

                        System.Diagnostics.Debug.WriteLine($"[SaveSession] Channel {channel.ChannelNo}: StartEnergy={session.StartEnergy}, LastEnergy={session.LastEnergy}, Status={session.Status}");
                        return; // 성공
                    }
                    catch (IOException)
                    {
                        if (retry < MaxRetries - 1)
                        {
                            Thread.Sleep(RetryDelayMs);
                        }
                        else
                        {
                            throw new Exception($"Failed to save session for channel {channel.ChannelNo} after {MaxRetries} retries.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 충전 세션 상태 로드
        /// </summary>
        public static ChargingSessionState LoadSession(int connectorId)
        {
            lock (_fileLock)
            {
                return LoadSessionInternal(connectorId);
            }
        }

        /// <summary>
        /// lock 내부에서 호출되는 실제 로드 로직 (재진입 방지)
        /// </summary>
        private static ChargingSessionState LoadSessionInternal(int connectorId)
        {
            try
            {
                string filePath = Path.Combine(SessionDirectory, $"{SessionFilePrefix}_{connectorId}.json");
                if (!File.Exists(filePath))
                    return null;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(fs))
                {
                    string json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<ChargingSessionState>(json);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 모든 충전 세션 상태 로드
        /// </summary>
        public static List<ChargingSessionState> LoadAllSessions()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!Directory.Exists(SessionDirectory))
                    {
                        return new List<ChargingSessionState>();
                    }

                    var files = Directory.GetFiles(SessionDirectory, $"{SessionFilePrefix}_*.json");
                    var sessions = new List<ChargingSessionState>();

                    foreach (var file in files)
                    {
                        try
                        {
                            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (var reader = new StreamReader(fs))
                            {
                                string json = reader.ReadToEnd();
                                var session = JsonConvert.DeserializeObject<ChargingSessionState>(json);
                                if (session != null)
                                    sessions.Add(session);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to load session from {file}: {ex.Message}");
                        }
                    }

                    return sessions;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load sessions: {ex.Message}");
                    return new List<ChargingSessionState>();
                }
            }
        }

        /// <summary>
        /// 충전 세션 상태 삭제
        /// </summary>
        public static void DeleteSession(int connectorId)
        {
            lock (_fileLock)
            {
                try
                {
                    string filePath = Path.Combine(SessionDirectory, $"{SessionFilePrefix}_{connectorId}.json");
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    // 잔여 임시 파일도 정리
                    string tempPath = Path.Combine(SessionDirectory, $"{SessionFilePrefix}_{connectorId}.tmp");
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // 무시
                }
            }
        }

        /// <summary>
        /// 충전 중인지 확인
        /// </summary>
        public static bool IsCharging(ChargerChannel channel)
        {
            return channel != null &&
                   channel.CurrentSequence == ChargeSequence.Charging &&
                   channel.ChargingStartTime != DateTime.MaxValue &&
                   channel.ChargingEndTime == DateTime.MinValue;
        }
    }
}

