using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace EvChargerUI.Services.Database
{
    /// <summary>
    /// 전송 로그 저장 및 재전송 대상 조회/업데이트 전담 리포지토리
    /// </summary>
    public class OfflineTxRepository
    {
        private readonly SqliteService _sqlite;

        public OfflineTxRepository(SqliteService sqlite)
        {
            _sqlite = sqlite;
        }

        public long Insert(string messageType, string addUrl, string stationId, string chargerId, string requestJson, string status)
        {
            const string sql = @"INSERT INTO TransmissionLog(message_type, add_url, station_id, charger_id, request_json, status, created_at)
VALUES(@t, @u, @s, @c, @j, @st, @createdAt); SELECT last_insert_rowid();";
            var id = _sqlite.ExecuteScalar(sql,
                new SQLiteParameter("@t", messageType),
                new SQLiteParameter("@u", addUrl),
                new SQLiteParameter("@s", stationId),
                new SQLiteParameter("@c", (object)chargerId ?? DBNull.Value),
                new SQLiteParameter("@j", requestJson),
                new SQLiteParameter("@st", status),
                new SQLiteParameter("@createdAt", GetKstNowString())
            );
            return Convert.ToInt64(id);
        }

        public int MarkSent(long id)
        {
            /*
            const string sql = "UPDATE TransmissionLog SET status='sent', last_retry_time=datetime('now') WHERE id=@id";
            return _sqlite.ExecuteNonQuery(sql, new SQLiteParameter("@id", id));
            */
            const string sql = "UPDATE TransmissionLog SET status='sent', last_retry_time=@ts WHERE id=@id";
            return _sqlite.ExecuteNonQuery(sql,
                new SQLiteParameter("@id", id),
                new SQLiteParameter("@ts", GetKstNowString()));
        }

        public int BumpRetry(long id)
        {
            /*
            const string sql = "UPDATE TransmissionLog SET status='sent', last_retry_time=datetime('now') WHERE id=@id";
            return _sqlite.ExecuteNonQuery(sql, new SQLiteParameter("@id", id));
            */
            const string sql = "UPDATE TransmissionLog SET retry_count=retry_count+1, last_retry_time=@ts WHERE id=@id";
            return _sqlite.ExecuteNonQuery(sql,
                new SQLiteParameter("@id", id),
                new SQLiteParameter("@ts", GetKstNowString()));
        }

        public int PurgeOlderThanDays(int days)
        {
            const string sql = "DELETE FROM TransmissionLog WHERE created_at < datetime('now', @delta)";
            // SQLite interval 표현: '-30 days' 형태
            return _sqlite.ExecuteNonQuery(sql, new SQLiteParameter("@delta", $"-{days} days"));
        }

        public sealed class TransmissionItem
        {
            public long Id { get; set; }
            public string MessageType { get; set; }
            public string AddUrl { get; set; }
            public string StationId { get; set; }
            public string ChargerId { get; set; }
            public string RequestJson { get; set; }
        }

        public IEnumerable<TransmissionItem> GetPending(int limit)
        {
            const string sql = @"SELECT id, message_type, add_url, station_id, charger_id, request_json
FROM TransmissionLog WHERE status='pending' ORDER BY created_at ASC LIMIT @lim";
            var dt = _sqlite.Query(sql, new SQLiteParameter("@lim", limit));
            foreach (DataRow row in dt.Rows)
            {
                yield return new TransmissionItem
                {
                    Id = Convert.ToInt64(row["id"]),
                    MessageType = Convert.ToString(row["message_type"]),
                    AddUrl = Convert.ToString(row["add_url"]),
                    StationId = Convert.ToString(row["station_id"]),
                    ChargerId = row["charger_id"] == DBNull.Value ? null : Convert.ToString(row["charger_id"]),
                    RequestJson = Convert.ToString(row["request_json"])
                };
            }
        }

        private static string GetKstNowString()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                return now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }
}


