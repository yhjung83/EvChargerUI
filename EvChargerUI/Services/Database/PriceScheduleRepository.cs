using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace EvChargerUI.Services.Database
{
    /// <summary>
    /// 누리집으로부터 수신한 단가 스케줄을 DB에 저장/조회합니다.
    /// - apply_date ~ end_date 범위 기반으로 현재 활성 스케줄 조회
    /// - create_date DESC 우선순위로 중복 스케줄 처리
    /// </summary>
    public class PriceScheduleRepository
    {
        private readonly SqliteService _db;

        public PriceScheduleRepository(SqliteService db)
        {
            _db = db;
        }

        /// <summary>
        /// 단가 스케줄을 저장합니다. create_date가 같으면 덮어씁니다.
        /// end_date가 null/빈 문자열이면 "99991231235959"(무기한)로 저장합니다.
        /// charger_id는 수신 데이터 그대로 저장하되 조회 시에는 station_id 기준으로만 처리합니다.
        /// </summary>
        public void Upsert(string stationId, string chargerId, string createDate,
                           string applyDate, string endDate, double[] prices)
        {
            if (string.IsNullOrEmpty(createDate))
                createDate = DateTime.Now.ToString("yyyyMMddHHmmss");
            if (string.IsNullOrEmpty(applyDate))
                applyDate = DateTime.Now.ToString("yyyyMMddHHmmss");
            if (string.IsNullOrEmpty(endDate))
                endDate = "99991231235959";

            string pricesJson = JsonConvert.SerializeObject(prices);
            string sql = @"
INSERT OR REPLACE INTO PriceSchedules
    (station_id, charger_id, create_date, apply_date, end_date, prices_json)
VALUES
    (@sid, @cid, @cdate, @adate, @edate, @prices)";
            _db.ExecuteNonQuery(sql,
                new SQLiteParameter("@sid",    stationId),
                new SQLiteParameter("@cid",    chargerId),
                new SQLiteParameter("@cdate",  createDate),
                new SQLiteParameter("@adate",  applyDate),
                new SQLiteParameter("@edate",  endDate),
                new SQLiteParameter("@prices", pricesJson));
        }

        /// <summary>
        /// 현재 시각 기준으로 적용 중인 스케줄을 반환합니다. (station_id 기준)
        /// apply_date &lt;= now &lt; end_date 이고 create_date가 가장 최신인 행을 반환합니다.
        /// </summary>
        /// <param name="nowStr">현재 시각 문자열 (yyyyMMddHHmmss)</param>
        /// <returns>prices 배열(24개). 활성 스케줄 없으면 null.</returns>
        public double[] GetActive(string stationId, string nowStr)
        {
            string sql = @"
SELECT prices_json FROM PriceSchedules
WHERE station_id = @sid
  AND apply_date <= @now
  AND end_date   > @now
ORDER BY create_date DESC
LIMIT 1";
            object result = _db.ExecuteScalar(sql,
                new SQLiteParameter("@sid", stationId),
                new SQLiteParameter("@now", nowStr));

            if (result == null || result == System.DBNull.Value)
                return null;

            return JsonConvert.DeserializeObject<double[]>((string)result);
        }

        /// <summary>
        /// 현재 시각 기준으로 적용 중인 스케줄의 전체 정보를 반환합니다. (station_id 기준)
        /// create_date가 가장 최신인 활성 스케줄을 반환합니다.
        /// </summary>
        public PriceScheduleRow GetActiveSchedule(string stationId, string nowStr)
        {
            // applied_at 컬럼 존재 여부 확인
            bool hasAppliedAtColumn = true;
            try
            {
                var checkSql = "SELECT COUNT(*) FROM pragma_table_info('PriceSchedules') WHERE name='applied_at';";
                long columnCount = (long)_db.ExecuteScalar(checkSql);
                hasAppliedAtColumn = columnCount > 0;
            }
            catch
            {
                hasAppliedAtColumn = false;
            }

            // 활성 범위(apply_date <= now < end_date) 내에서 create_date가 가장 최신인 스케줄 반환
            string sql = hasAppliedAtColumn
                ? @"SELECT id, station_id, charger_id, create_date, apply_date, end_date, prices_json, applied_at
FROM PriceSchedules
WHERE station_id = @sid
  AND apply_date <= @now
  AND end_date   > @now
ORDER BY create_date DESC
LIMIT 1"
                : @"SELECT id, station_id, charger_id, create_date, apply_date, end_date, prices_json
FROM PriceSchedules
WHERE station_id = @sid
  AND apply_date <= @now
  AND end_date   > @now
ORDER BY create_date DESC
LIMIT 1";

            var table = _db.Query(sql,
                new SQLiteParameter("@sid", stationId),
                new SQLiteParameter("@now", nowStr));

            if (table.Rows.Count == 0)
                return null;

            var row = table.Rows[0];
            string appliedAt = null;
            if (hasAppliedAtColumn && row.Table.Columns.Contains("applied_at") && row["applied_at"] != DBNull.Value)
            {
                appliedAt = (string)row["applied_at"];
            }

            return new PriceScheduleRow
            {
                Id         = Convert.ToInt64(row["id"]),
                StationId  = (string)row["station_id"],
                ChargerId  = (string)row["charger_id"],
                CreateDate = (string)row["create_date"],
                ApplyDate  = (string)row["apply_date"],
                EndDate    = (string)row["end_date"],
                Prices     = JsonConvert.DeserializeObject<double[]>((string)row["prices_json"]),
                AppliedAt  = appliedAt
            };
        }

        /// <summary>
        /// 다음으로 적용될 스케줄의 apply_date를 반환합니다. (station_id 기준)
        /// </summary>
        /// <param name="nowStr">현재 시각 문자열 (yyyyMMddHHmmss)</param>
        /// <returns>apply_date 문자열. 없으면 null.</returns>
        public string GetNextApplyDate(string stationId, string nowStr)
        {
            string sql = @"
SELECT apply_date FROM PriceSchedules
WHERE station_id = @sid
  AND apply_date > @now
ORDER BY apply_date ASC
LIMIT 1";
            object result = _db.ExecuteScalar(sql,
                new SQLiteParameter("@sid", stationId),
                new SQLiteParameter("@now", nowStr));

            return (result == null || result == System.DBNull.Value) ? null : (string)result;
        }

        /// <summary>
        /// 만료된 스케줄 중 end_date 기준 1개월 이상 경과한 항목만 삭제합니다. (station_id 기준)
        /// </summary>
        public void PurgeExpired(string stationId, string nowStr)
        {
            string oneMonthAgoStr = DateTime.Now.AddMonths(-1).ToString("yyyyMMddHHmmss");
            string sql = @"
DELETE FROM PriceSchedules
WHERE station_id = @sid
  AND end_date < @oneMonthAgo";
            _db.ExecuteNonQuery(sql,
                new SQLiteParameter("@sid", stationId),
                new SQLiteParameter("@oneMonthAgo", oneMonthAgoStr));
        }

        /// <summary>
        /// 특정 스케줄의 적용 시점을 기록합니다.
        /// </summary>
        public void MarkAsApplied(string stationId, string createDate)
        {
            string appliedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string sql = @"
UPDATE PriceSchedules
SET applied_at = @appliedAt
WHERE station_id = @sid
  AND create_date = @cdate";
            _db.ExecuteNonQuery(sql,
                new SQLiteParameter("@appliedAt", appliedAt),
                new SQLiteParameter("@sid", stationId),
                new SQLiteParameter("@cdate", createDate));
        }

        /// <summary>
        /// 해당 충전소에 스케줄 레코드가 하나라도 존재하는지 반환합니다. (station_id 기준)
        /// (만료 포함, PurgeExpired 호출 전에 사용)
        /// </summary>
        public bool HasAny(string stationId)
        {
            string sql = @"
SELECT COUNT(*) FROM PriceSchedules
WHERE station_id = @sid";
            object result = _db.ExecuteScalar(sql,
                new SQLiteParameter("@sid", stationId));
            return result != null && result != DBNull.Value && Convert.ToInt64(result) > 0;
        }

        /// <summary>
        /// 해당 충전소의 모든 스케줄 목록을 반환합니다. (디버그/모니터링용, station_id 기준)
        /// </summary>
        public List<PriceScheduleRow> GetAll(string stationId)
        {
            // applied_at 컬럼 존재 여부 확인
            bool hasAppliedAtColumn = true;
            try
            {
                var checkSql = "SELECT COUNT(*) FROM pragma_table_info('PriceSchedules') WHERE name='applied_at';";
                long columnCount = (long)_db.ExecuteScalar(checkSql);
                hasAppliedAtColumn = columnCount > 0;
            }
            catch
            {
                hasAppliedAtColumn = false;
            }

            string sql = hasAppliedAtColumn
                ? @"SELECT id, station_id, charger_id, create_date, apply_date, end_date, prices_json, applied_at
FROM PriceSchedules
WHERE station_id = @sid
ORDER BY apply_date ASC, create_date DESC"
                : @"SELECT id, station_id, charger_id, create_date, apply_date, end_date, prices_json
FROM PriceSchedules
WHERE station_id = @sid
ORDER BY apply_date ASC, create_date DESC";

            var table = _db.Query(sql, new SQLiteParameter("@sid", stationId));

            var list = new List<PriceScheduleRow>();
            foreach (System.Data.DataRow row in table.Rows)
            {
                string appliedAt = null;
                if (hasAppliedAtColumn && row.Table.Columns.Contains("applied_at") && row["applied_at"] != DBNull.Value)
                {
                    appliedAt = (string)row["applied_at"];
                }

                list.Add(new PriceScheduleRow
                {
                    Id         = Convert.ToInt64(row["id"]),
                    StationId  = (string)row["station_id"],
                    ChargerId  = (string)row["charger_id"],
                    CreateDate = (string)row["create_date"],
                    ApplyDate  = (string)row["apply_date"],
                    EndDate    = (string)row["end_date"],
                    Prices     = JsonConvert.DeserializeObject<double[]>((string)row["prices_json"]),
                    AppliedAt  = appliedAt
                });
            }
            return list;
        }
    }

    public class PriceScheduleRow
    {
        public long     Id         { get; set; }
        public string   StationId  { get; set; }
        public string   ChargerId  { get; set; }
        public string   CreateDate { get; set; }
        public string   ApplyDate  { get; set; }
        public string   EndDate    { get; set; }
        public double[] Prices     { get; set; }
        public string   AppliedAt  { get; set; }
    }
}
