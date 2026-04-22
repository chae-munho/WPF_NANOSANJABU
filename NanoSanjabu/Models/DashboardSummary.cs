namespace NanoSanjabu.Models
{
    public class DashboardSummary
    {
        // 적층완료보드에 안착 완료된 총 Glass 수 = LAMINATED 그룹 수 * 5
        public int ProductionCount { get; set; }

        // 현재 미사용
        public double PassRate { get; set; }

        // 현재 미사용
        public double DefectRate { get; set; }

        // 완료된 tray_run 수 (상판 1회, 하판 1회 각각 1건)
        public int CompletedTrayCount { get; set; }
    }
}