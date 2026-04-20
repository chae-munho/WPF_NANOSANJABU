using MySqlConnector;
using NanoSanjabu.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NanoSanjabu.Services
{
    public class MesRepository
    {
        private readonly DatabaseService _databaseService;

        public MesRepository(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        private static Brush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public async Task<long> GetOrCreateActiveTrayRunAsync()
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string selectSql = @"
SELECT id
FROM tray_run
WHERE status = 'RUNNING'
ORDER BY id DESC
LIMIT 1;";

            await using (var selectCommand = new MySqlCommand(selectSql, connection))
            {
                var result = await selectCommand.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt64(result);
                }
            }

            return await CreateNewTrayRunAsync(connection);
        }

        private async Task<long> CreateNewTrayRunAsync(MySqlConnection connection)
        {
            string trayLotNo = $"TRAY-{DateTime.Now:yyyyMMdd-HHmmss}";

            const string insertRunSql = @"
INSERT INTO tray_run (tray_lot_no, status, started_at)
VALUES (@trayLotNo, 'RUNNING', NOW());
SELECT LAST_INSERT_ID();";

            long trayRunId;
            await using (var cmd = new MySqlCommand(insertRunSql, connection))
            {
                cmd.Parameters.AddWithValue("@trayLotNo", trayLotNo);
                trayRunId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            // row = 1 상단, row = 5 하단
            // 실제 lot 번호는 좌하단부터 시작
            for (int row = 1; row <= 5; row++)
            {
                for (int col = 1; col <= 10; col++)
                {
                    int slotNo = ((5 - row) * 10) + col;
                    string glassLotNo = $"{trayLotNo}-{slotNo:00}";

                    const string insertSlotSql = @"
INSERT INTO tray_slot (tray_run_id, slot_no, row_no, col_no, glass_lot_no, status)
VALUES (@trayRunId, @slotNo, @rowNo, @colNo, @glassLotNo, 'WAITING');";

                    await using var slotCmd = new MySqlCommand(insertSlotSql, connection);
                    slotCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                    slotCmd.Parameters.AddWithValue("@slotNo", slotNo);
                    slotCmd.Parameters.AddWithValue("@rowNo", row);
                    slotCmd.Parameters.AddWithValue("@colNo", col);
                    slotCmd.Parameters.AddWithValue("@glassLotNo", glassLotNo);
                    await slotCmd.ExecuteNonQueryAsync();
                }
            }

            for (int groupNo = 1; groupNo <= 10; groupNo++)
            {
                int startSlotNo = ((groupNo - 1) * 5) + 1;
                int endSlotNo = groupNo * 5;
                string groupLotNo = $"{trayLotNo}-G{groupNo:00}";

                const string insertGroupSql = @"
INSERT INTO stack_group (tray_run_id, group_no, start_slot_no, end_slot_no, group_lot_no, status)
VALUES (@trayRunId, @groupNo, @startSlotNo, @endSlotNo, @groupLotNo, 'WAITING');";

                await using var groupCmd = new MySqlCommand(insertGroupSql, connection);
                groupCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                groupCmd.Parameters.AddWithValue("@groupNo", groupNo);
                groupCmd.Parameters.AddWithValue("@startSlotNo", startSlotNo);
                groupCmd.Parameters.AddWithValue("@endSlotNo", endSlotNo);
                groupCmd.Parameters.AddWithValue("@groupLotNo", groupLotNo);
                await groupCmd.ExecuteNonQueryAsync();
            }

            return trayRunId;
        }

        public async Task<string> GetTrayLotNoAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"SELECT tray_lot_no FROM tray_run WHERE id = @id LIMIT 1;";
            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", trayRunId);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "";
        }

        public async Task MarkSlotRunningAsync(long trayRunId, int slotNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE tray_slot
SET status = CASE
    WHEN status = 'WAITING' THEN 'RUNNING'
    ELSE status
END,
updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND slot_no = @slotNo;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@slotNo", slotNo);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarkGlassLoadedAsync(long trayRunId, int slotNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE tray_slot
SET status = 'GLASS_LOADED',
    loading_at = NOW(),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND slot_no = @slotNo;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@slotNo", slotNo);
            await cmd.ExecuteNonQueryAsync();

            await InsertEventAsync(trayRunId, slotNo, null, "GLASS_LOADED", "M858", "ON", $"슬롯 {slotNo} Glass 안착 완료");
        }

        public async Task MarkNanoDoneAsync(long trayRunId, int slotNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE tray_slot
SET status = 'COMPLETE',
    nano_done_at = NOW(),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND slot_no = @slotNo;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@slotNo", slotNo);
            await cmd.ExecuteNonQueryAsync();

            const string updateRunSql = @"
UPDATE tray_run
SET completed_slots = (
    SELECT COUNT(*)
    FROM tray_slot
    WHERE tray_run_id = @trayRunId
      AND status = 'COMPLETE'
),
updated_at = NOW()
WHERE id = @trayRunId;";

            await using var updateRunCmd = new MySqlCommand(updateRunSql, connection);
            updateRunCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            await updateRunCmd.ExecuteNonQueryAsync();

            await InsertEventAsync(trayRunId, slotNo, null, "NANO_DONE", "M863", "ON", $"슬롯 {slotNo} Nano 분사 완료");
        }

        public async Task MarkDryStartedAsync(long trayRunId, string zone)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE tray_run
SET dry_started_at = IFNULL(dry_started_at, NOW()),
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            await cmd.ExecuteNonQueryAsync();

            await InsertEventAsync(trayRunId, null, null, "DRY_START", zone, "ON", $"{zone} 건조 시작");
        }

        public async Task MarkDryCompletedAsync(long trayRunId, string zone)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE tray_run
SET dry_completed_at = NOW(),
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            await cmd.ExecuteNonQueryAsync();

            await InsertEventAsync(trayRunId, null, null, "DRY_COMPLETE", zone, "ON", $"{zone} 건조 완료");
        }

        public async Task MarkStackGroupRunningAsync(long trayRunId, int groupNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE stack_group
SET status = CASE
    WHEN status = 'WAITING' THEN 'RUNNING'
    ELSE status
END,
stacking_started_at = IFNULL(stacking_started_at, NOW()),
updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND group_no = @groupNo;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@groupNo", groupNo);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarkUvRunningAsync(long trayRunId, int groupNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE stack_group
SET uv_started_at = IFNULL(uv_started_at, NOW()),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND group_no = @groupNo;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@groupNo", groupNo);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarkStackGroupCompletedAsync(long trayRunId, int groupNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            int startSlotNo = ((groupNo - 1) * 5) + 1;
            int endSlotNo = groupNo * 5;

            const string updateGroupSql = @"
UPDATE stack_group
SET status = 'COMPLETE',
    uv_completed_at = NOW(),
    completed_at = NOW(),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND group_no = @groupNo;";

            await using var groupCmd = new MySqlCommand(updateGroupSql, connection);
            groupCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            groupCmd.Parameters.AddWithValue("@groupNo", groupNo);
            await groupCmd.ExecuteNonQueryAsync();

            const string updateSlotSql = @"
UPDATE tray_slot
SET stacked_at = NOW(),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND slot_no BETWEEN @startSlotNo AND @endSlotNo;";

            await using var slotCmd = new MySqlCommand(updateSlotSql, connection);
            slotCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            slotCmd.Parameters.AddWithValue("@startSlotNo", startSlotNo);
            slotCmd.Parameters.AddWithValue("@endSlotNo", endSlotNo);
            await slotCmd.ExecuteNonQueryAsync();

            const string updateRunSql = @"
UPDATE tray_run
SET completed_groups = (
    SELECT COUNT(*)
    FROM stack_group
    WHERE tray_run_id = @trayRunId
      AND status = 'COMPLETE'
),
updated_at = NOW()
WHERE id = @trayRunId;";

            await using var runCmd = new MySqlCommand(updateRunSql, connection);
            runCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            await runCmd.ExecuteNonQueryAsync();

            await InsertEventAsync(trayRunId, null, groupNo, "STACK_GROUP_COMPLETE", "M937", "ON", $"그룹 {groupNo} 적층 완료");
        }

        public async Task<bool> CompleteTrayIfFinishedAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string countSql = @"
SELECT COUNT(*)
FROM stack_group
WHERE tray_run_id = @trayRunId
  AND status = 'COMPLETE';";

            int completeCount;
            await using (var countCmd = new MySqlCommand(countSql, connection))
            {
                countCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                completeCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            if (completeCount < 10)
            {
                return false;
            }

            const string sql = @"
UPDATE tray_run
SET status = 'COMPLETE',
    completed_at = NOW(),
    updated_at = NOW()
WHERE id = @trayRunId
  AND status <> 'COMPLETE';";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            await cmd.ExecuteNonQueryAsync();

            await InsertEventAsync(trayRunId, null, null, "TRAY_COMPLETE", null, null, "트레이 전체 완료");
            return true;
        }

        public async Task HandleAlarmAsync(long? trayRunId, short errorCode)
        {
            string errorName = PlcAddressMapper.GetD0ErrorText(errorCode);

            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            if (errorCode <= 0)
            {
                const string closeSql = @"
UPDATE equipment_alarm
SET status = 'CLOSED',
    cleared_at = NOW(),
    updated_at = NOW()
WHERE status = 'OPEN';";

                await using var closeCmd = new MySqlCommand(closeSql, connection);
                await closeCmd.ExecuteNonQueryAsync();
                return;
            }

            const string existsSql = @"
SELECT COUNT(*)
FROM equipment_alarm
WHERE status = 'OPEN'
  AND error_code = @errorCode;";

            await using (var existsCmd = new MySqlCommand(existsSql, connection))
            {
                existsCmd.Parameters.AddWithValue("@errorCode", errorCode);
                int existsCount = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());
                if (existsCount > 0)
                {
                    return;
                }
            }

            const string insertSql = @"
INSERT INTO equipment_alarm (tray_run_id, error_code, error_name, occurred_at, status)
VALUES (@trayRunId, @errorCode, @errorName, NOW(), 'OPEN');";

            await using var cmd = new MySqlCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId.HasValue ? trayRunId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@errorCode", errorCode);
            cmd.Parameters.AddWithValue("@errorName", errorName);
            await cmd.ExecuteNonQueryAsync();

            await InsertEventAsync(trayRunId, null, null, "ALARM", "D0", errorCode.ToString(), errorName);
        }

        public async Task InsertEventAsync(long? trayRunId, int? slotNo, int? groupNo, string eventType, string? plcAddress, string? eventValue, string? message)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
INSERT INTO process_event (
    tray_run_id,
    slot_no,
    group_no,
    event_type,
    plc_address,
    event_value,
    message,
    event_time
)
VALUES (
    @trayRunId,
    @slotNo,
    @groupNo,
    @eventType,
    @plcAddress,
    @eventValue,
    @message,
    NOW()
);";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId.HasValue ? trayRunId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@slotNo", slotNo.HasValue ? slotNo.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@groupNo", groupNo.HasValue ? groupNo.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@eventType", eventType);
            cmd.Parameters.AddWithValue("@plcAddress", plcAddress ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@eventValue", eventValue ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@message", message ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM stack_group WHERE status = 'COMPLETE') AS production_count,
    0 AS pass_rate,
    0 AS defect_rate,
    (SELECT COUNT(*) FROM tray_run WHERE status = 'COMPLETE') AS completed_tray_count;";

            await using var cmd = new MySqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();

            var summary = new DashboardSummary();
            if (await reader.ReadAsync())
            {
                summary.ProductionCount = Convert.ToInt32(reader["production_count"]);
                summary.PassRate = Convert.ToDouble(reader["pass_rate"]);
                summary.DefectRate = Convert.ToDouble(reader["defect_rate"]);
                summary.CompletedTrayCount = Convert.ToInt32(reader["completed_tray_count"]);
            }

            return summary;
        }

        public async Task<HistorySummary> GetHistorySummaryAsync()
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM stack_group) AS total_lot_count,
    (SELECT COUNT(*) FROM stack_group WHERE status = 'COMPLETE') AS total_produced_unit,
    (
        SELECT IFNULL(AVG(TIMESTAMPDIFF(MINUTE, started_at, completed_at)), 0)
        FROM tray_run
        WHERE completed_at IS NOT NULL
    ) AS avg_process_minutes,
    0 AS rework_lot_count;";

            await using var cmd = new MySqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();

            var summary = new HistorySummary();
            if (await reader.ReadAsync())
            {
                summary.TotalLotCount = Convert.ToInt32(reader["total_lot_count"]);
                summary.TotalProducedUnit = Convert.ToInt32(reader["total_produced_unit"]);
                summary.AverageProcessMinutes = Convert.ToInt32(reader["avg_process_minutes"]);
                summary.ReworkLotCount = Convert.ToInt32(reader["rework_lot_count"]);
            }

            return summary;
        }

        public async Task<List<ReportItem>> GetRecentReportsAsync(int count)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
SELECT event_time, message
FROM process_event
ORDER BY id DESC
LIMIT @count;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@count", count);

            var items = new List<ReportItem>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                items.Add(new ReportItem
                {
                    EventTime = reader.GetDateTime("event_time"),
                    Message = reader["message"]?.ToString() ?? ""
                });
            }

            return items;
        }

        public async Task<List<InputSlotState>> GetInputSlotsAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
SELECT slot_no, row_no, col_no, status,
       TIMESTAMPDIFF(MINUTE, COALESCE(loading_at, created_at), NOW()) AS elapsed_min
FROM tray_slot
WHERE tray_run_id = @trayRunId
ORDER BY row_no ASC, col_no ASC;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);

            var items = new List<InputSlotState>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string status = reader["status"]?.ToString() ?? SlotStatus.Waiting;
                (Brush brush, string statusText) = GetSlotBrushAndText(status);

                items.Add(new InputSlotState
                {
                    SlotNo = Convert.ToInt32(reader["slot_no"]),
                    RowNo = Convert.ToInt32(reader["row_no"]),
                    ColNo = Convert.ToInt32(reader["col_no"]),
                    LotText = $"Lot {Convert.ToInt32(reader["slot_no"])}",
                    StatusText = statusText,
                    TimeText = $"TIME: {Convert.ToInt32(reader["elapsed_min"])}m",
                    StatusBrush = brush
                });
            }

            return items;
        }

        public async Task<List<StackGroupState>> GetStackGroupsAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
SELECT group_no, start_slot_no, end_slot_no, status,
       TIMESTAMPDIFF(MINUTE, COALESCE(stacking_started_at, created_at), NOW()) AS elapsed_min
FROM stack_group
WHERE tray_run_id = @trayRunId
ORDER BY group_no ASC;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);

            var items = new List<StackGroupState>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int groupNo = Convert.ToInt32(reader["group_no"]);
                int startSlotNo = Convert.ToInt32(reader["start_slot_no"]);
                int endSlotNo = Convert.ToInt32(reader["end_slot_no"]);
                string status = reader["status"]?.ToString() ?? SlotStatus.Waiting;

                (Brush brush, string statusText, string modeText) = GetGroupBrushAndText(status);

                items.Add(new StackGroupState
                {
                    GroupNo = groupNo,
                    StartSlotNo = startSlotNo,
                    EndSlotNo = endSlotNo,
                    LotText = $"[ Lot {groupNo} ]",
                    RangeText = $"LOT A{startSlotNo}~{endSlotNo}",
                    StatusText = statusText,
                    ModeText = modeText,
                    TimeText = status == SlotStatus.Waiting
                        ? "작업 없음"
                        : $"총 {Convert.ToInt32(reader["elapsed_min"])}분",
                    StatusBrush = brush
                });
            }

            return items;
        }

        private static (Brush brush, string text) GetSlotBrushAndText(string status)
        {
            return status switch
            {
                SlotStatus.Running => (CreateFrozenBrush("#FF3B30"), "STATUS: RUN"),
                SlotStatus.GlassLoaded => (CreateFrozenBrush("#F4C542"), "STATUS: LOAD"),
                SlotStatus.Complete => (CreateFrozenBrush("#D7F04A"), "STATUS: COMPLETE"),
                _ => (CreateFrozenBrush("#D9D9D9"), "STATUS: WAITING")
            };
        }

        private static (Brush brush, string statusText, string modeText) GetGroupBrushAndText(string status)
        {
            return status switch
            {
                SlotStatus.Running => (
                    CreateFrozenBrush("#FF3B30"),
                    "합성 중",
                    "RUN"),
                SlotStatus.Complete => (
                    CreateFrozenBrush("#D7F04A"),
                    "합성 완료",
                    "COMPLETE"),
                _ => (
                    CreateFrozenBrush("#D9D9D9"),
                    "LOT 대기중",
                    "IDLE")
            };
        }
    }
}