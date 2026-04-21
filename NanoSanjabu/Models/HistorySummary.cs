namespace NanoSanjabu.Models
{
    public class HistorySummary
    {
        public int TotalLotCount { get; set; }            // 전체 적층 그룹 수
        public int TotalProducedUnit { get; set; }        // 완료 생산 Glass 수
        public int AverageProcessMinutes { get; set; }    // 평균 tray 완료 시간(분)
        public int ReworkLotCount { get; set; }           // 현재 미사용
    }
}