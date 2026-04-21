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
                        StatusCode = SlotStatus.Waiting,
                        LotText = $"{row}행 {col}열",
                        StatusText = "WAITING",
                        TimeText = "0s",
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

                PlcAddressMapper.GetDefaultStackOutPosition(groupNo, out int rowNo, out int colNo);

                items.Add(new StackGroupState
                {
                    GroupNo = groupNo,
                    StartSlotNo = startSlotNo,
                    EndSlotNo = endSlotNo,
                    RowNo = rowNo,
                    ColNo = colNo,
                    StatusCode = SlotStatus.Waiting,
                    LotText = $"{rowNo}행 {colNo}열",
                    RangeText = $"LOT {startSlotNo}~{endSlotNo}",
                    StatusText = "WAITING",
                    ModeText = $"G{groupNo:00}",
                    TimeText = "0s",
                    StatusBrush = CreateFrozenBrush("#D9D9D9")
                });
            }

            return items;
        }
    }
}