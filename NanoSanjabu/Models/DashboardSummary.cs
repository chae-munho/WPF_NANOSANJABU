namespace NanoSanjabu.Models
{
    public class DashboardSummary
    {
        public int ProductionCount { get; set; }          // 완료 그룹 수 * 5
        public double PassRate { get; set; }              // 현재 미사용
        public double DefectRate { get; set; }            // 현재 미사용
        public int CompletedTrayCount { get; set; }       // 완료 tray 수
    }
}