using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace EvChargerUI.Services.Database
{
    /// <summary>
    /// SQLite 데이터베이스 초기화 및 기본 실행 유틸리티를 제공합니다.
    /// - 데이터베이스 파일 생성
    /// - 테이블 생성(존재하지 않으면)
    /// - 커맨드 실행/조회 헬퍼
    /// </summary>
    public class SqliteService
    {
        private readonly string _dbFilePath;
        private readonly string _connectionString;

        public SqliteService(string dbFilePath)
        {
            _dbFilePath = dbFilePath;
            _connectionString = $"Data Source={_dbFilePath};Version=3;Journal Mode=WAL;Foreign Keys=True;";
        }

        /// <summary>
        /// 데이터베이스 파일과 기본 테이블을 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            EnsureDatabaseFile();
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                // 예시용 기본 테이블들 (필요 시 확장)
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ChargingSessions (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    StationId       TEXT NOT NULL,
    ChargerId       TEXT NOT NULL,
    ChannelNo       INTEGER NOT NULL,
    StartTime       TEXT NOT NULL,
    EndTime         TEXT,
    EnergyWh        INTEGER DEFAULT 0,
    Cost            INTEGER DEFAULT 0,
    OrderNo         TEXT,
    CreatedAt       TEXT DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS IX_ChargingSessions_OrderNo ON ChargingSessions(OrderNo);

CREATE TABLE IF NOT EXISTS KeyValues (
    Key     TEXT PRIMARY KEY,
    Value   TEXT
);

CREATE TABLE IF NOT EXISTS TransmissionLog (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    message_type    TEXT NOT NULL,
    add_url         TEXT NOT NULL,
    station_id      TEXT NOT NULL,
    charger_id      TEXT,
    request_json    TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'pending', -- pending | sent
    retry_count     INTEGER NOT NULL DEFAULT 0,
    last_retry_time TEXT,
    created_at      TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS IX_TransmissionLog_Status ON TransmissionLog(status);
CREATE INDEX IF NOT EXISTS IX_TransmissionLog_CreatedAt ON TransmissionLog(created_at);

CREATE TABLE IF NOT EXISTS PriceSchedules (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    station_id  TEXT NOT NULL,
    charger_id  TEXT NOT NULL,
    create_date TEXT NOT NULL,
    apply_date  TEXT NOT NULL,
    end_date    TEXT NOT NULL,
    prices_json TEXT NOT NULL,
    applied_at  TEXT,
    created_at  TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_PriceSchedules ON PriceSchedules(station_id, create_date);
CREATE INDEX IF NOT EXISTS IX_PriceSchedules_Apply ON PriceSchedules(station_id, apply_date, end_date);

CREATE TABLE IF NOT EXISTS PriceChangeLog (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    station_id    TEXT NOT NULL,
    hour_index    INTEGER NOT NULL,
    old_price     FLOAT NOT NULL,
    new_price     FLOAT NOT NULL,
    change_source TEXT NOT NULL,
    changed_at    TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS IX_PriceChangeLog_Station ON PriceChangeLog(station_id, changed_at);
";
                cmd.ExecuteNonQuery();

                // PriceSchedules 테이블에 applied_at 컬럼이 없으면 추가 (기존 DB 마이그레이션)
                cmd.CommandText = @"
SELECT COUNT(*) FROM pragma_table_info('PriceSchedules') WHERE name='applied_at';
";
                long columnExists = (long)cmd.ExecuteScalar();
                if (columnExists == 0)
                {
                    cmd.CommandText = @"ALTER TABLE PriceSchedules ADD COLUMN applied_at TEXT;";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// INSERT/UPDATE/DELETE 등 변경 쿼리를 실행합니다.
        /// </summary>
        public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 단일 값(예: count(*))을 조회합니다.
        /// </summary>
        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// SELECT 결과를 DataTable로 반환합니다.
        /// </summary>
        public DataTable Query(string sql, params SQLiteParameter[] parameters)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                using (var da = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    da.Fill(table);
                    return table;
                }
            }
        }

        private void EnsureDatabaseFile()
        {
            var dir = Path.GetDirectoryName(_dbFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(_dbFilePath))
            {
                SQLiteConnection.CreateFile(_dbFilePath);
            }
        }

        private SQLiteConnection OpenConnection()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }
    }
}


