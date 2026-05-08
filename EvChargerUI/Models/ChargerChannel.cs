using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvChargerUI.Commons.Enum;
using EvChargerUI.Domains;
using EvChargerUI.Views.Popup;

namespace EvChargerUI.Models
{
    // ========== 구간별 과금: 단가 변경 이력 클래스 ==========
    /// <summary>
    /// 충전 중 단가 변경 시점의 정보를 저장하는 클래스
    /// </summary>
    public class UnitCostChangeRecord
    {
        /// <summary>단가 변경 시점의 PowerMeter (kWh)</summary>
        public double PowerMeter { get; set; }
        /// <summary>해당 구간에 적용된 단가 (원/kWh)</summary>
        public float UnitCost { get; set; }
        /// <summary>해당 시점까지의 누적 금액 (원)</summary>
        public int AccumulatedCost { get; set; }
    }
    // ========== 구간별 과금: 클래스 정의 끝 ==========

    public class ChargerChannel
    {
        public ChargerChannel(int channelNo, string stationId, string chargerId, string qrCode)
        {
            StationId = stationId;
            ChannelNo = channelNo;
            ChargerId = chargerId;
            QrCode = "https://www.ev.or.kr/bridge?code="+qrCode;
            CurrentSequence = ChargeSequence.SelectConnector;
        }

        public string StationId { get; private set; }
        public string ChargerId { get; private set; }
        public int ChannelNo { get; private set; }
        public ChargeSequence CurrentSequence { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public int UserSetChargeAmount { get; set; }
        public int ChargeAmount { get; set; }
        public int CancelChargeAmount { get; set; }
        public PaymentInfo  PrePaymentInfo { get; set; }
        public string MembershipNo { get; set; }
        
        public string QrTid { get; set; } // QR 결제 시 tid 저장

        public DateTime ChargingStartTime { get; set; }

        public DateTime ChargingEndTime { get; set; }

        public string ChargeEndCallbackPhoneNumber {  get; set; }   
        
        public string ReservationPhoneNo { get; set; }
        public string ReservationNo { get; set; }
        public bool IsReservationSmsSent { get; set; }

        public int ChargeTime
        {
            get
            {
                if (ChargingStartTime != DateTime.MaxValue)
                {
                    if (ChargingEndTime != DateTime.MinValue)
                    {
                        return (int)(ChargingEndTime - ChargingStartTime).TotalSeconds;
                    }
                    else
                    {
                        return (int)(DateTime.Now - ChargingStartTime).TotalSeconds;
                    }
                }
                else
                {
                    return 0;
                }
            }
                
        }

        public string OrderNo
        {
            get
            {
                return StationId + ChargerId + "_" + ChargingStartTime.ToString("yyyyMMddHHmmss");
            }
        }

        public double BasePowerMeter { get; set; }

        public double FinalPowerMeter { get; set; }

        public bool IsPaymentCancelSuccess { get; set; }
        public string CsName => "ECP" + StationId + ChargerId;
        public string QrCode { get; private set; }

        public static float DefaultUnitCost { get; set; } = 347.2f;
        public string MembershipNoValidationCode { get; set; }
        public string MemberCompanyCode { get; set; }
        public float CurrentUserUnitCost { get; set; }
        public bool IsChargerPaymentRequired { get; set; }
        public int ChargingSelect { get; set; } = 1; // 커넥터 타입 (표준: 0=AC3, 1=DC콤보, 2=차데모, 기본값=1)
        public bool IsWaitForConnectorPlugInCancelled { get; set; } = false; // WaitForConnectorPlugIn 취소 플래그

        // ========== 구간별 과금 지원을 위한 필드 추가 ==========
        /// <summary>단가 변경 이력: 각 단가 변경 시점의 PowerMeter, 단가, 누적 금액</summary>
        public List<UnitCostChangeRecord> UnitCostChangeHistory { get; set; }
        /// <summary>현재 구간의 시작 PowerMeter (kWh)</summary>
        public double CurrentSegmentStartPowerMeter { get; set; }
        /// <summary>현재 구간 이전까지의 누적 금액 (원)</summary>
        public int AccumulatedCostBeforeCurrentSegment { get; set; }
        // ========== 구간별 과금 필드 추가 끝 ==========

        public void Init()
        {
            CurrentSequence = ChargeSequence.SelectConnector;
            PaymentMethod = PaymentMethod.None;

            UserSetChargeAmount = -1;
            ChargeAmount = 0;
            CancelChargeAmount = 0;
            PrePaymentInfo = null;
            MembershipNo = null;
            ChargingStartTime = DateTime.MaxValue;
            ChargingEndTime = DateTime.MinValue;

            MembershipNoValidationCode = null;
            MemberCompanyCode = null;
            // 현재 시간대의 요금을 기본값으로 사용
            int currentHour = DateTime.Now.Hour;
            CurrentUserUnitCost = Commons.Settings.AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
            IsChargerPaymentRequired = true;
            // ChargingSelect는 settings.ini에서 설정된 값을 유지 (기본값으로 덮어쓰지 않음)
            QrTid = null;
            IsWaitForConnectorPlugInCancelled = false;

            // ========== 구간별 과금 필드 초기화 ==========
            UnitCostChangeHistory = new List<UnitCostChangeRecord>();
            CurrentSegmentStartPowerMeter = 0.0;
            AccumulatedCostBeforeCurrentSegment = 0;
            // ========== 구간별 과금 필드 초기화 끝 ==========
        }

        public void InitPaymentInfo()
        {
            PaymentMethod = PaymentMethod.None;

            UserSetChargeAmount = -1;
            ChargeAmount = 0;
            CancelChargeAmount = 0;
            PrePaymentInfo = null;
            MembershipNo = null;
            ChargingStartTime = DateTime.MaxValue;
            ChargingEndTime = DateTime.MinValue;

            MembershipNoValidationCode = null;
            MemberCompanyCode = null;
            // 현재 시간대의 요금을 기본값으로 사용
            int currentHour = DateTime.Now.Hour;
            CurrentUserUnitCost = Commons.Settings.AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
            IsChargerPaymentRequired = true;
            // ChargingSelect는 settings.ini에서 설정된 값을 유지 (기본값으로 덮어쓰지 않음)
            QrTid = null;
            IsWaitForConnectorPlugInCancelled = false;

            // ========== 구간별 과금 필드 초기화 ==========
            UnitCostChangeHistory = new List<UnitCostChangeRecord>();
            CurrentSegmentStartPowerMeter = 0.0;
            AccumulatedCostBeforeCurrentSegment = 0;
            // ========== 구간별 과금 필드 초기화 끝 ==========
        }

        public void InitReservationInfo()
        {
            ReservationNo = null;
            ReservationPhoneNo = null;
            IsReservationSmsSent = false;
        }
    }
}
