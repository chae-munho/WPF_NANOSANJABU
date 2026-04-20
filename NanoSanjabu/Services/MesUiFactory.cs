using NanoSanjabu.Models;
using System.Collections.Generic;
using System.Windows.Media;

namespace NanoSanjabu.Services
{
    public static class MesUiFactory
    {
        private static Brush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public static List<InputSlotState> CreateDefaultInputSlots()
        {
            var items = new List<InputSlotState>();

            // row = 1 이 화면 상단, row = 5 가 화면 하단
            // 실제 번호는 좌하단 Lot1 이므로:
            // slotNo = ((5 - row) * 10) + col
            for (int row = 1; row <= 5; row++)
            {
                for (int col = 1; col <= 10; col++)
                {
                    int slotNo = ((5 - row) * 10) + col;

                    items.Add(new InputSlotState
                    {
                        SlotNo = slotNo,
                        RowNo = row,
                        ColNo = col,
                        LotText = $"Lot {slotNo}",
                        StatusText = "STATUS: WAITING",
                        TimeText = "TIME: 0m",
                        StatusBrush = CreateFrozenBrush("#D9D9D9")
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
                    StatusBrush = CreateFrozenBrush("#D9D9D9")
                });
            }

            return items;
        }
    }
}