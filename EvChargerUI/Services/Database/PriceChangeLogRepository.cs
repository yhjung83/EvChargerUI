using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace EvChargerUI.Services.Database
{
    /// <summary>
    /// PriceChangeLog 테이블을 조회하는 Repository
    /// </summary>
    public class PriceChangeLogRepository
    {
        private readonly SqliteService _sqlite;

        public PriceChangeLogRepository(SqliteService sqlite)
        {
            _sqlite = sqlite;
        }

        /// <summary>
        /// 특정 충전소의 단가 변경 로그를 조회합니다.
        /// </summary>
        public List<PriceChangeLogRow> GetAll(string stationId)
        {
            var result = new List<PriceChangeLogRow>();

            string sql = @"
SELECT 
    id,
    station_id,
    hour_index,
    old_price,
    new_price,
    change_source,
    changed_at
FROM PriceChangeLog
WHERE station_id = @stationId
ORDER BY changed_at DESC, id DESC
";

            DataTable table = _sqlite.Query(sql, new SQLiteParameter("@stationId", stationId));

            foreach (DataRow row in table.Rows)
            {
                result.Add(new PriceChangeLogRow
                {
                    Id = Convert.ToInt64(row["id"]),
                    StationId = Convert.ToString(row["station_id"]),
                    HourIndex = Convert.ToInt32(row["hour_index"]),
                    OldPrice = Convert.ToDouble(row["old_price"]),
                    NewPrice = Convert.ToDouble(row["new_price"]),
                    ChangeSource = Convert.ToString(row["change_source"]),
                    ChangedAt = Convert.ToString(row["changed_at"])
                });
            }

            return result;
        }

        /// <summary>
        /// 최근 N개의 변경 로그를 조회합니다.
        /// </summary>
        public List<PriceChangeLogRow> GetRecent(string stationId, int count)
        {
            var result = new List<PriceChangeLogRow>();

            string sql = @"
SELECT 
    id,
    station_id,
    hour_index,
    old_price,
    new_price,
    change_source,
    changed_at
FROM PriceChangeLog
WHERE station_id = @stationId
ORDER BY changed_at DESC, id DESC
LIMIT @count
";

            DataTable table = _sqlite.Query(sql, 
                new SQLiteParameter("@stationId", stationId),
                new SQLiteParameter("@count", count));

            foreach (DataRow row in table.Rows)
            {
                result.Add(new PriceChangeLogRow
                {
                    Id = Convert.ToInt64(row["id"]),
                    StationId = Convert.ToString(row["station_id"]),
                    HourIndex = Convert.ToInt32(row["hour_index"]),
                    OldPrice = Convert.ToDouble(row["old_price"]),
                    NewPrice = Convert.ToDouble(row["new_price"]),
                    ChangeSource = Convert.ToString(row["change_source"]),
                    ChangedAt = Convert.ToString(row["changed_at"])
                });
            }

            return result;
        }
    }

    /// <summary>
    /// PriceChangeLog 테이블의 단일 행을 나타냅니다.
    /// </summary>
    public class PriceChangeLogRow
    {
        public long Id { get; set; }
        public string StationId { get; set; }
        public int HourIndex { get; set; }
        public double OldPrice { get; set; }
        public double NewPrice { get; set; }
        public string ChangeSource { get; set; }  // "INI" 또는 "DB"
        public string ChangedAt { get; set; }
    }
}
