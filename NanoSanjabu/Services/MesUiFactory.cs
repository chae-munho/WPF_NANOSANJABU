using NanoSanjabu.Models;
using System.Collections.Generic;
using System.Windows.Media;

namespace NanoSanjabu.Services
{
    public static class MesUiFactory
    {
        public static List<InputSlotState> CreateDefaultInputSlots()
        {
            var items = new List<InputSlotState>();

            for (int col = 1; col <= 10; col++)
            {
                for (int row = 1; row <= 5; row++)
                {
                    int slotNo = ((col - 1) * 5) + (6 - row);

                    items.Add(new InputSlotState
                    {
                        SlotNo = slotNo,
                        RowNo = row,
                        ColNo = col,
                        LotText = $"Lot {slotNo}",
                        StatusText = "STATUS: WAITING",
                        TimeText = "TIME: 0m",
                        StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9D9D9"))
                    });
                }
            }

            return items;
        }

        public static List<StackGroupState> CreateDefaultStackGroups()
        {
            var items = new List<StackGroupState>();

            for (int groupNo = 1; groupNo <= 10; groupNo++)
            {
                int startSlotNo = ((groupNo - 1) * 5) + 1;
                int endSlotNo = groupNo * 5;

                items.Add(new StackGroupState
                {
                    GroupNo = groupNo,
                    StartSlotNo = startSlotNo,
                    EndSlotNo = endSlotNo,
                    LotText = $"[ Lot {groupNo} ]",
                    RangeText = $"LOT A{startSlotNo}~{endSlotNo}",
                    StatusText = "LOT 대기중",
                    ModeText = "IDLE",
                    TimeText = "작업 없음",
                    StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9D9D9"))
                });
            }

            return items;
        }
    }
}