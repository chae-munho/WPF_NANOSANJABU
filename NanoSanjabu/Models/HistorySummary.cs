namespace NanoSanjabu.Models
{
    public class HistorySummary
    {
        // 현재 기준: 적층부까지 진행된 총 Glass 수
        public int TotalLotCount { get; set; }

        // 현재 미사용
        public int TotalProducedUnit { get; set; }

        // 완료된 tray_run 평균 소요 시간(분)
        public int AverageProcessMinutes { get; set; }

        // 현재 미사용
        public int ReworkLotCount { get; set; }
    }
}