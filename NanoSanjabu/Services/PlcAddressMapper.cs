namespace NanoSanjabu.Services
{
    public static class PlcAddressMapper
    {
        // Loader X = D60(열), Loader Y = D61(행)
        // 실제 50슬롯 판 좌표 범위: D60 1~10, D61 1~5
        public static bool TryGetInputSlot(short loaderX, short loaderY, out int rowNo, out int colNo, out int slotNo)
        {
            rowNo = 0;
            colNo = 0;
            slotNo = 0;

            if (loaderX < 1 || loaderX > 10)
                return false;

            if (loaderY < 1 || loaderY > 5)
                return false;

            colNo = loaderX;
            rowNo = loaderY;
            slotNo = ((rowNo - 1) * 10) + colNo;
            return true;
        }

        // 적층 source pick 판단
        // D65 = Transfer X(열 1~10)
        // D66 = Unloader X(행 1~5)
        public static bool TryGetStackPickSlot(short transferX, short unloaderX, out int rowNo, out int colNo, out int slotNo)
        {
            rowNo = 0;
            colNo = 0;
            slotNo = 0;

            if (transferX < 1 || transferX > 10)
                return false;

            if (unloaderX < 1 || unloaderX > 5)
                return false;

            colNo = transferX;
            rowNo = unloaderX;
            slotNo = ((rowNo - 1) * 10) + colNo;
            return true;
        }

        // 적층완료 보드 위치
        // D66 11~15 => 1~5열
        // D67 11~12 => 1~2행
        public static bool TryGetStackBoardPosition(short unloaderX, short unloaderY, out int rowNo, out int colNo)
        {
            rowNo = 0;
            colNo = 0;

            if (unloaderX < 11 || unloaderX > 15)
                return false;

            if (unloaderY < 11 || unloaderY > 12)
                return false;

            colNo = unloaderX - 10;
            rowNo = unloaderY - 10;
            return true;
        }

        public static void GetDefaultStackBoardPosition(int groupNo, out int rowNo, out int colNo)
        {
            if (groupNo < 1) groupNo = 1;
            if (groupNo > 10) groupNo = 10;

            rowNo = groupNo <= 5 ? 1 : 2;
            colNo = ((groupNo - 1) % 5) + 1;
        }

        // D26=적층완료 배출수(1~10), D20=현재 그룹 내 투입수(1~5)
        public static int GetCurrentGroupNo(short stackOutCount, short stackInputCount)
        {
            if (stackOutCount >= 10)
                return 10;

            if (stackInputCount > 0)
                return stackOutCount + 1;

            int next = stackOutCount + 1;
            return next > 10 ? 10 : next;
        }

        public static string GetD0ErrorText(short value)
        {
            return value switch
            {
                1 => "L-X Servo Alarm",
                2 => "L-Y Servo Alarm",
                3 => "L-Z Servo Alarm",
                4 => "ELV Servo Alarm",
                5 => "T-X Servo Alarm",
                6 => "T-Y Servo Alarm",
                7 => "U-X Servo Alarm",
                8 => "U-Y Servo Alarm",
                9 => "U-Z Servo Alarm",
                10 => "DDM Servo Alarm",
                11 => "Emergency Stop",
                12 => "Loader Z-Axis is Not in Up state",
                13 => "Unloader Z-Axis is Not in Up state",
                14 => "Loader Z Vacuum Error",
                15 => "Unoader Z Vacuum Error",
                _ => "No Error / Unknown"
            };
        }

        public static string GetTrayTypeText(string trayType)
        {
            return trayType switch
            {
                "UPPER" => "상판",
                "LOWER" => "하판",
                _ => trayType
            };
        }
    }
}