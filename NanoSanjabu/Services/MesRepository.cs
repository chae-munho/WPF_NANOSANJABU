using MySqlConnector;
using NanoSanjabu.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NanoSanjabu.Services
{
    public class MesRepository
    {
        private readonly DatabaseService _databaseService;

        public MesRepository(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<long> GetOrCreateCurrentTrayRunAsync()
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string selectSql = @"
SELECT id
FROM tray_run
WHERE status <> 'COMPLETE'
ORDER BY id DESC
LIMIT 1;";

            await using (var selectCmd = new MySqlCommand(selectSql, connection))
            {
                var result = await selectCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt64(result);
                }
            }

            string nextTrayType = await GetNextTrayTypeAsync(connection);
            return await CreateNewTrayRunAsync(connection, nextTrayType);
        }

        private static async Task<string> GetNextTrayTypeAsync(MySqlConnection connection)
        {
            const string sql = @"
SELECT tray_type
FROM tray_run
ORDER BY id DESC
LIMIT 1;";

            await using var cmd = new MySqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return "UPPER";

            string lastTrayType = result.ToString() ?? "UPPER";
            return lastTrayType == "UPPER" ? "LOWER" : "UPPER";
        }

        private async Task<long> CreateNewTrayRunAsync(MySqlConnection connection, string trayType)
        {
            string trayRunNo = $"{trayType}-{DateTime.Now:yyyyMMdd-HHmmss}";

            const string insertRunSql = @"
INSERT INTO tray_run
(
    tray_run_no,
    tray_type,
    status,
    started_at
)
VALUES
(
    @trayRunNo,
    @trayType,
    'RUNNING',
    NOW()
);

SELECT LAST_INSERT_ID();";

            long trayRunId;
            await using (var cmd = new MySqlCommand(insertRunSql, connection))
            {
                cmd.Parameters.AddWithValue("@trayRunNo", trayRunNo);
                cmd.Parameters.AddWithValue("@trayType", trayType);
                trayRunId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            for (int row = 1; row <= 5; row++)
            {
                for (int col = 1; col <= 10; col++)
                {
                    int slotNo = ((row - 1) * 10) + col;
                    string glassLotNo = $"{trayRunNo}-{slotNo:00}";

                    const string insertSlotSql = @"
INSERT INTO tray_slot
(
    tray_run_id,
    slot_no,
    row_no,
    col_no,
    glass_no,
    glass_lot_no,
    status
)
VALUES
(
    @trayRunId,
    @slotNo,
    @rowNo,
    @colNo,
    @glassNo,
    @glassLotNo,
    'WAITING'
);";

                    await using var slotCmd = new MySqlCommand(insertSlotSql, connection);
                    slotCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                    slotCmd.Parameters.AddWithValue("@slotNo", slotNo);
                    slotCmd.Parameters.AddWithValue("@rowNo", row);
                    slotCmd.Parameters.AddWithValue("@colNo", col);
                    slotCmd.Parameters.AddWithValue("@glassNo", slotNo);
                    slotCmd.Parameters.AddWithValue("@glassLotNo", glassLotNo);
                    await slotCmd.ExecuteNonQueryAsync();
                }
            }

            for (int groupNo = 1; groupNo <= 10; groupNo++)
            {
                PlcAddressMapper.GetDefaultStackBoardPosition(groupNo, out int boardRowNo, out int boardColNo);

                const string insertGroupSql = @"
INSERT INTO stack_group
(
    tray_run_id,
    group_no,
    board_row_no,
    board_col_no,
    status
)
VALUES
(
    @trayRunId,
    @groupNo,
    @boardRowNo,
    @boardColNo,
    'WAITING'
);";

                await using var groupCmd = new MySqlCommand(insertGroupSql, connection);
                groupCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                groupCmd.Parameters.AddWithValue("@groupNo", groupNo);
                groupCmd.Parameters.AddWithValue("@boardRowNo", boardRowNo);
                groupCmd.Parameters.AddWithValue("@boardColNo", boardColNo);
                await groupCmd.ExecuteNonQueryAsync();
            }

            await InsertEventAsync(
                trayRunId,
                null,
                null,
                trayType,
                null,
                null,
                "TRAY_START",
                null,
                null,
                $"{PlcAddressMapper.GetTrayTypeText(trayType)} 트레이 작업 시작");

            return trayRunId;
        }

        public async Task<(string TrayRunNo, string TrayType)> GetTrayRunInfoAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
SELECT tray_run_no, tray_type
FROM tray_run
WHERE id = @id
LIMIT 1;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", trayRunId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string trayRunNo = reader["tray_run_no"]?.ToString() ?? "";
                string trayType = reader["tray_type"]?.ToString() ?? "";
                return (trayRunNo, trayType);
            }

            return ("", "");
        }

        public async Task MarkSlotLoadingAsync(long trayRunId, int slotNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
UPDATE tray_slot
SET status = CASE
        WHEN status = 'WAITING' THEN 'LOADING'
        ELSE status
    END,
    loading_started_at = CASE
        WHEN loading_started_at IS NULL THEN NOW()
        ELSE loading_started_at
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

            int rowNo = 0;
            int colNo = 0;
            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string updateSlotSql = @"
UPDATE tray_slot
SET status = 'LOADING',
    loading_started_at = IFNULL(loading_started_at, NOW()),
    loading_completed_at = IFNULL(loading_completed_at, NOW()),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND slot_no = @slotNo;";

            await using (var slotCmd = new MySqlCommand(updateSlotSql, connection))
            {
                slotCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                slotCmd.Parameters.AddWithValue("@slotNo", slotNo);
                await slotCmd.ExecuteNonQueryAsync();
            }

            const string updateRunSql = @"
UPDATE tray_run
SET loaded_slots = (
        SELECT COUNT(*)
        FROM tray_slot
        WHERE tray_run_id = @trayRunId
          AND loading_completed_at IS NOT NULL
    ),
    loading_completed_at = CASE
        WHEN (
            SELECT COUNT(*)
            FROM tray_slot
            WHERE tray_run_id = @trayRunId
              AND loading_completed_at IS NOT NULL
        ) >= 50
        THEN IFNULL(loading_completed_at, NOW())
        ELSE loading_completed_at
    END,
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using (var runCmd = new MySqlCommand(updateRunSql, connection))
            {
                runCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                await runCmd.ExecuteNonQueryAsync();
            }

            (rowNo, colNo) = await FillRowColAsync(connection, trayRunId, slotNo);

            await InsertEventAsync(
                trayRunId,
                slotNo,
                null,
                trayType,
                rowNo,
                colNo,
                "GLASS_LOADED",
                "M858",
                "ON",
                $"슬롯 {slotNo} Glass 안착 완료");
        }

        public async Task MarkNanoDoneAsync(long trayRunId, int slotNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            int rowNo = 0;
            int colNo = 0;
            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string updateSlotSql = @"
UPDATE tray_slot
SET status = 'COMPLETE',
    nano_completed_at = NOW(),
    slot_completed_at = NOW(),
    slot_elapsed_seconds = TIMESTAMPDIFF(
        SECOND,
        COALESCE(loading_started_at, created_at),
        NOW()
    ),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND slot_no = @slotNo;";

            await using (var slotCmd = new MySqlCommand(updateSlotSql, connection))
            {
                slotCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                slotCmd.Parameters.AddWithValue("@slotNo", slotNo);
                await slotCmd.ExecuteNonQueryAsync();
            }

            const string updateRunSql = @"
UPDATE tray_run
SET nano_completed_slots = (
        SELECT COUNT(*)
        FROM tray_slot
        WHERE tray_run_id = @trayRunId
          AND status = 'COMPLETE'
    ),
    nano_completed_at = CASE
        WHEN (
            SELECT COUNT(*)
            FROM tray_slot
            WHERE tray_run_id = @trayRunId
              AND status = 'COMPLETE'
        ) >= 50
        THEN IFNULL(nano_completed_at, NOW())
        ELSE nano_completed_at
    END,
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using (var runCmd = new MySqlCommand(updateRunSql, connection))
            {
                runCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                await runCmd.ExecuteNonQueryAsync();
            }

            (rowNo, colNo) = await FillRowColAsync(connection, trayRunId, slotNo);

            await InsertEventAsync(
                trayRunId,
                slotNo,
                null,
                trayType,
                rowNo,
                colNo,
                "NANO_DONE",
                "M863",
                "ON",
                $"슬롯 {slotNo} Nano 완료");
        }

        public async Task MarkDryStartedAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string sql = @"
UPDATE tray_run
SET status = 'DRYING',
    dry_started_at = IFNULL(dry_started_at, NOW()),
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            await cmd.ExecuteNonQueryAsync();

            string plcAddress = trayType == "UPPER" ? "L1" : "L2";

            await InsertEventAsync(
                trayRunId,
                null,
                null,
                trayType,
                null,
                null,
                "DRY_START",
                plcAddress,
                "ON",
                $"{PlcAddressMapper.GetTrayTypeText(trayType)} 건조 시작");
        }

        public async Task MarkDryCompletedAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string sql = @"
UPDATE tray_run
SET status = 'DRY_COMPLETE',
    dry_completed_at = NOW(),
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            await cmd.ExecuteNonQueryAsync();

            string plcAddress = trayType == "UPPER" ? "L3" : "L4";

            await InsertEventAsync(
                trayRunId,
                null,
                null,
                trayType,
                null,
                null,
                "DRY_COMPLETE",
                plcAddress,
                "ON",
                $"{PlcAddressMapper.GetTrayTypeText(trayType)} 건조 완료");
        }

        public async Task RegisterStackPickAsync(long trayRunId, int groupNo, int slotNo, int pickOrder)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            long stackGroupId = await GetStackGroupIdAsync(connection, trayRunId, groupNo);
            long traySlotId = await GetTraySlotIdAsync(connection, trayRunId, slotNo);

            if (stackGroupId == 0 || traySlotId == 0)
                return;

            const string insertItemSql = @"
INSERT INTO stack_group_item
(
    stack_group_id,
    tray_slot_id,
    pick_order,
    picked_at
)
SELECT
    @stackGroupId,
    @traySlotId,
    @pickOrder,
    NOW()
WHERE NOT EXISTS
(
    SELECT 1
    FROM stack_group_item
    WHERE stack_group_id = @stackGroupId
      AND pick_order = @pickOrder
);";

            await using (var insertCmd = new MySqlCommand(insertItemSql, connection))
            {
                insertCmd.Parameters.AddWithValue("@stackGroupId", stackGroupId);
                insertCmd.Parameters.AddWithValue("@traySlotId", traySlotId);
                insertCmd.Parameters.AddWithValue("@pickOrder", pickOrder);
                await insertCmd.ExecuteNonQueryAsync();
            }

            const string updateSlotSql = @"
UPDATE tray_slot
SET status = 'UNLOADED',
    unloaded_at = IFNULL(unloaded_at, NOW()),
    updated_at = NOW()
WHERE id = @traySlotId;";

            await using (var updateSlotCmd = new MySqlCommand(updateSlotSql, connection))
            {
                updateSlotCmd.Parameters.AddWithValue("@traySlotId", traySlotId);
                await updateSlotCmd.ExecuteNonQueryAsync();
            }

            const string updateRunSql = @"
UPDATE tray_run
SET unloaded_slots = (
        SELECT COUNT(*)
        FROM tray_slot
        WHERE tray_run_id = @trayRunId
          AND status = 'UNLOADED'
    ),
    stacking_started_at = IFNULL(stacking_started_at, NOW()),
    status = CASE
        WHEN status IN ('RUNNING', 'DRY_COMPLETE') THEN 'STACKING'
        ELSE status
    END,
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using (var runCmd = new MySqlCommand(updateRunSql, connection))
            {
                runCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                await runCmd.ExecuteNonQueryAsync();
            }

            var (rowNo, colNo) = await FillRowColAsync(connection, trayRunId, slotNo);

            await InsertEventAsync(
                trayRunId,
                slotNo,
                groupNo,
                trayType,
                rowNo,
                colNo,
                "STACK_PICK",
                "M906",
                pickOrder.ToString(),
                $"그룹 {groupNo} pick_order {pickOrder}, 슬롯 {slotNo} pick");
        }

        public async Task InsertDottingEventAsync(long trayRunId, int groupNo, short dottingCount)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();
            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            await InsertEventAsync(
                trayRunId,
                null,
                groupNo,
                trayType,
                null,
                null,
                "UV_DOTTING_DONE",
                "M991",
                dottingCount.ToString(),
                $"그룹 {groupNo} Dotting 완료 ({dottingCount}/4)");
        }

        public async Task InsertUvStartEventAsync(long trayRunId, int groupNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();
            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            await InsertEventAsync(
                trayRunId,
                null,
                groupNo,
                trayType,
                null,
                null,
                "UV_START",
                "M922",
                "ON",
                $"그룹 {groupNo} UV 시작");
        }

        public async Task InsertUvFinishEventAsync(long trayRunId, int groupNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();
            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            await InsertEventAsync(
                trayRunId,
                null,
                groupNo,
                trayType,
                null,
                null,
                "UV_FINISH",
                "M922",
                "OFF",
                $"그룹 {groupNo} UV 종료");
        }

        public async Task MarkStackGroupLaminatedAsync(long trayRunId, int groupNo, int boardRowNo, int boardColNo)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string updateGroupSql = @"
UPDATE stack_group
SET status = 'LAMINATED',
    board_row_no = @boardRowNo,
    board_col_no = @boardColNo,
    laminated_at = NOW(),
    updated_at = NOW()
WHERE tray_run_id = @trayRunId
  AND group_no = @groupNo;";

            await using (var groupCmd = new MySqlCommand(updateGroupSql, connection))
            {
                groupCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                groupCmd.Parameters.AddWithValue("@groupNo", groupNo);
                groupCmd.Parameters.AddWithValue("@boardRowNo", boardRowNo);
                groupCmd.Parameters.AddWithValue("@boardColNo", boardColNo);
                await groupCmd.ExecuteNonQueryAsync();
            }

            const string updateRunSql = @"
UPDATE tray_run
SET laminated_groups = (
        SELECT COUNT(*)
        FROM stack_group
        WHERE tray_run_id = @trayRunId
          AND status = 'LAMINATED'
    ),
    updated_at = NOW()
WHERE id = @trayRunId;";

            await using (var runCmd = new MySqlCommand(updateRunSql, connection))
            {
                runCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                await runCmd.ExecuteNonQueryAsync();
            }

            string memberText = await GetStackGroupMembersTextAsync(connection, trayRunId, groupNo);

            await InsertEventAsync(
                trayRunId,
                null,
                groupNo,
                trayType,
                null,
                null,
                "STACK_GROUP_LAMINATED",
                "M937",
                "ON",
                $"그룹 {groupNo} 안착 완료 / {boardRowNo}행 {boardColNo}열 / {memberText}");
        }

        public async Task<bool> CompleteTrayIfFinishedAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string countSql = @"
SELECT COUNT(*)
FROM stack_group
WHERE tray_run_id = @trayRunId
  AND status = 'LAMINATED';";

            int laminatedCount;
            await using (var countCmd = new MySqlCommand(countSql, connection))
            {
                countCmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                laminatedCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            if (laminatedCount < 10)
                return false;

            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string updateSql = @"
UPDATE tray_run
SET status = 'COMPLETE',
    completed_at = NOW(),
    updated_at = NOW()
WHERE id = @trayRunId
  AND status <> 'COMPLETE';";

            await using (var cmd = new MySqlCommand(updateSql, connection))
            {
                cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
                await cmd.ExecuteNonQueryAsync();
            }

            await InsertEventAsync(
                trayRunId,
                null,
                null,
                trayType,
                null,
                null,
                "TRAY_COMPLETE",
                null,
                null,
                $"{PlcAddressMapper.GetTrayTypeText(trayType)} 트레이 전체 완료");

            return true;
        }

        public async Task HandleAlarmAsync(long? trayRunId, short errorCode)
        {
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

                await InsertEventAsync(
                    trayRunId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "ALARM_CLEAR",
                    "D0",
                    "0",
                    "알람 해제");

                return;
            }

            string errorName = PlcAddressMapper.GetD0ErrorText(errorCode);

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
                    return;
            }

            const string closeDifferentSql = @"
UPDATE equipment_alarm
SET status = 'CLOSED',
    cleared_at = NOW(),
    updated_at = NOW()
WHERE status = 'OPEN'
  AND error_code <> @errorCode;";

            await using (var closeDifferentCmd = new MySqlCommand(closeDifferentSql, connection))
            {
                closeDifferentCmd.Parameters.AddWithValue("@errorCode", errorCode);
                await closeDifferentCmd.ExecuteNonQueryAsync();
            }

            const string insertSql = @"
INSERT INTO equipment_alarm
(
    tray_run_id,
    error_code,
    error_name,
    occurred_at,
    status
)
VALUES
(
    @trayRunId,
    @errorCode,
    @errorName,
    NOW(),
    'OPEN'
);";

            await using var insertCmd = new MySqlCommand(insertSql, connection);
            insertCmd.Parameters.AddWithValue("@trayRunId", trayRunId.HasValue ? trayRunId.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@errorCode", errorCode);
            insertCmd.Parameters.AddWithValue("@errorName", errorName);
            await insertCmd.ExecuteNonQueryAsync();

            await InsertEventAsync(
                trayRunId,
                null,
                null,
                null,
                null,
                null,
                "ALARM",
                "D0",
                errorCode.ToString(),
                errorName);
        }

        public async Task InsertEventAsync(
            long? trayRunId,
            int? slotNo,
            int? groupNo,
            string? trayType,
            int? rowNo,
            int? colNo,
            string eventType,
            string? plcAddress,
            string? eventValue,
            string? message)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            const string sql = @"
INSERT INTO process_event
(
    tray_run_id,
    tray_type,
    row_no,
    col_no,
    slot_no,
    group_no,
    event_type,
    plc_address,
    event_value,
    message,
    event_time
)
VALUES
(
    @trayRunId,
    @trayType,
    @rowNo,
    @colNo,
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
            cmd.Parameters.AddWithValue("@trayType", trayType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@rowNo", rowNo.HasValue ? rowNo.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@colNo", colNo.HasValue ? colNo.Value : DBNull.Value);
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
    (SELECT IFNULL(COUNT(*) * 5, 0) FROM stack_group WHERE status = 'LAMINATED') AS production_count,
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
    (SELECT IFNULL(COUNT(*) * 5, 0) FROM stack_group WHERE status = 'LAMINATED') AS total_lot_count,
    0 AS total_produced_unit,
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
ORDER BY event_time DESC, id DESC
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

            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string sql = @"
SELECT
    slot_no,
    row_no,
    col_no,
    status,
    glass_no,
    glass_lot_no,
    slot_elapsed_seconds,
    slot_completed_at,
    TIMESTAMPDIFF(
        SECOND,
        COALESCE(loading_started_at, created_at),
        NOW()
    ) AS current_elapsed_sec
FROM tray_slot
WHERE tray_run_id = @trayRunId
ORDER BY row_no ASC, col_no ASC;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);

            var items = new List<InputSlotState>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string status = reader["status"]?.ToString() ?? InputSlotStatus.Waiting;

                int rowNo = Convert.ToInt32(reader["row_no"]);
                int colNo = Convert.ToInt32(reader["col_no"]);
                int slotNo = Convert.ToInt32(reader["slot_no"]);

                int currentElapsedSec = Convert.ToInt32(reader["current_elapsed_sec"]);
                int? savedElapsedSec = reader["slot_elapsed_seconds"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["slot_elapsed_seconds"]);

                DateTime? completedAt = reader["slot_completed_at"] == DBNull.Value
                    ? null
                    : reader.GetDateTime("slot_completed_at");

                string timeText = status switch
                {
                    InputSlotStatus.Complete => $"{(savedElapsedSec ?? 0)}s",
                    InputSlotStatus.Unloaded => $"{(savedElapsedSec ?? 0)}s",
                    InputSlotStatus.Loading => $"{currentElapsedSec}s",
                    _ => "0s"
                };

                string completedAtText = completedAt.HasValue
                    ? completedAt.Value.ToString("HH:mm:ss")
                    : "-";

                items.Add(new InputSlotState
                {
                    SlotNo = slotNo,
                    RowNo = rowNo,
                    ColNo = colNo,
                    TrayType = trayType,
                    StatusCode = status,
                    LotText = $"{rowNo}행 {colNo}열",
                    StatusText = MesUiFactory.GetInputSlotStatusText(status),
                    TimeText = timeText,
                    CompletedAtText = completedAtText,
                    StatusBrush = MesUiFactory.GetInputSlotBrush(status)
                });
            }

            return items;
        }

        public async Task<List<StackBoardCellState>> GetStackBoardCellsAsync(long trayRunId)
        {
            await using var connection = await _databaseService.CreateOpenConnectionAsync();

            string trayType = await GetTrayTypeAsync(connection, trayRunId);

            const string sql = @"
SELECT
    sg.group_no,
    sg.board_row_no,
    sg.board_col_no,
    sg.status,
    sg.laminated_at,
    (
        SELECT GROUP_CONCAT(CAST(ts.slot_no AS CHAR) ORDER BY sgi.pick_order SEPARATOR ', ')
        FROM stack_group_item sgi
        JOIN tray_slot ts
          ON ts.id = sgi.tray_slot_id
        WHERE sgi.stack_group_id = sg.id
    ) AS members
FROM stack_group sg
WHERE sg.tray_run_id = @trayRunId
ORDER BY sg.group_no ASC;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);

            var items = new List<StackBoardCellState>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int groupNo = Convert.ToInt32(reader["group_no"]);
                int rowNo = Convert.ToInt32(reader["board_row_no"]);
                int colNo = Convert.ToInt32(reader["board_col_no"]);
                string status = reader["status"]?.ToString() ?? StackBoardStatus.Waiting;
                string members = reader["members"]?.ToString() ?? "";

                DateTime? laminatedAt = reader["laminated_at"] == DBNull.Value
                    ? null
                    : reader.GetDateTime("laminated_at");

                items.Add(new StackBoardCellState
                {
                    GroupNo = groupNo,
                    RowNo = rowNo,
                    ColNo = colNo,
                    TrayType = trayType,
                    StatusCode = status,
                    LotText = $"{rowNo}행 {colNo}열",
                    MemberText = members,
                    StatusText = MesUiFactory.GetStackBoardStatusText(status),
                    TimeText = laminatedAt.HasValue ? laminatedAt.Value.ToString("HH:mm:ss") : "-",
                    StatusBrush = MesUiFactory.GetStackBoardBrush(status)
                });
            }

            return items;
        }

        private static async Task<string> GetTrayTypeAsync(MySqlConnection connection, long trayRunId)
        {
            const string sql = @"
SELECT tray_type
FROM tray_run
WHERE id = @trayRunId
LIMIT 1;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "";
        }

        private static async Task<(int RowNo, int ColNo)> FillRowColAsync(
     MySqlConnection connection,
     long trayRunId,
     int slotNo)
        {
            const string sql = @"
SELECT row_no, col_no
FROM tray_slot
WHERE tray_run_id = @trayRunId
  AND slot_no = @slotNo
LIMIT 1;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@slotNo", slotNo);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int rowNo = reader.GetInt32("row_no");
                int colNo = reader.GetInt32("col_no");
                return (rowNo, colNo);
            }

            return (0, 0);
        }

        private static async Task<long> GetStackGroupIdAsync(MySqlConnection connection, long trayRunId, int groupNo)
        {
            const string sql = @"
SELECT id
FROM stack_group
WHERE tray_run_id = @trayRunId
  AND group_no = @groupNo
LIMIT 1;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@groupNo", groupNo);

            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        private static async Task<long> GetTraySlotIdAsync(MySqlConnection connection, long trayRunId, int slotNo)
        {
            const string sql = @"
SELECT id
FROM tray_slot
WHERE tray_run_id = @trayRunId
  AND slot_no = @slotNo
LIMIT 1;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@slotNo", slotNo);

            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        private static async Task<string> GetStackGroupMembersTextAsync(MySqlConnection connection, long trayRunId, int groupNo)
        {
            const string sql = @"
SELECT GROUP_CONCAT(CAST(ts.slot_no AS CHAR) ORDER BY sgi.pick_order SEPARATOR ', ')
FROM stack_group_item sgi
JOIN stack_group sg
  ON sg.id = sgi.stack_group_id
JOIN tray_slot ts
  ON ts.id = sgi.tray_slot_id
WHERE sg.tray_run_id = @trayRunId
  AND sg.group_no = @groupNo;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@trayRunId", trayRunId);
            cmd.Parameters.AddWithValue("@groupNo", groupNo);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "";
        }
    }
}