namespace NanoSanjabu.Services
{
    public static class PlcAddressMapper
    {
        public static bool TryGetInputSlot(short loaderX, short loaderY, out int rowNo, out int colNo, out int slotNo)
        {
            rowNo = 0;
            colNo = 0;
            slotNo = 0;

            if (loaderX < 1 || loaderX > 10)
            {
                return false;
            }

            if (loaderY < 1 || loaderY > 5)
            {
                return false;
            }

            colNo = loaderX;
            rowNo = loaderY;

            // 실제 배치:
            // 좌하단 Lot1, 우하단 Lot10, 그 윗줄 Lot11
            slotNo = ((5 - rowNo) * 10) + colNo;
            return true;
        }

        public static int GetGroupNoFromSlotNo(int slotNo)
        {
            if (slotNo < 1)
            {
                return 1;
            }

            if (slotNo > 50)
            {
                return 10;
            }

            return ((slotNo - 1) / 5) + 1;
        }

        public static int GetCurrentStackGroup(short stackOutCount, short stackInputCount)
        {
            if (stackOutCount >= 10)
            {
                return 10;
            }

            if (stackInputCount > 0)
            {
                return stackOutCount + 1;
            }

            int groupNo = stackOutCount + 1;
            return groupNo > 10 ? 10 : groupNo;
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
    }
}