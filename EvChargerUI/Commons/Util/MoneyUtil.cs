using System;

namespace EvChargerUI.Commons.Util
{
    public static class MoneyUtil
    {
        /// <summary>
        /// 원단위(1원 자리) 절삭: 10원 단위로 내림 처리합니다.
        /// 예) 12345 -> 12340
        /// </summary>
        public static int TruncateWonUnit(int amount) => TruncateToUnit(amount, 10);

        /// <summary>
        /// 지정 단위로 절삭(내림) 처리합니다.
        /// unit=10이면 10원 단위, unit=100이면 100원 단위로 절삭됩니다.
        /// </summary>
        public static int TruncateToUnit(int amount, int unit)
        {
            if (unit <= 0) return amount;

            // 음수도 "절삭"이 기대대로 동작하도록 0 방향으로 버림 처리
            if (amount >= 0)
                return (amount / unit) * unit;

            int abs = Math.Abs(amount);
            return -((abs / unit) * unit);
        }
    }
}


