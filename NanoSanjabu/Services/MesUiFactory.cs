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
                    int slotNo = ((row - 1) * 10) + col;

                    items.Add(new InputSlotState
                    {
                        SlotNo = slotNo,
                        RowNo = row,
                        ColNo = col,
                        TrayType = "",
                        StatusCode = InputSlotStatus.Waiting,
                        LotText = $"{row}행 {col}열",
                        StatusText = "WAITING",
                        TimeText = "0s",
                        CompletedAtText = "-",
                        StatusBrush = CreateFrozenBrush("#D9D9D9")
                    });
                }
            }

            return items;
        }

        public static List<StackBoardCellState> CreateDefaultStackBoardCells()
        {
            var items = new List<StackBoardCellState>();

            for (int groupNo = 1; groupNo <= 10; groupNo++)
            {
                PlcAddressMapper.GetDefaultStackBoardPosition(groupNo, out int rowNo, out int colNo);

                items.Add(new StackBoardCellState
                {
                    GroupNo = groupNo,
                    RowNo = rowNo,
                    ColNo = colNo,
                    TrayType = "",
                    StatusCode = StackBoardStatus.Waiting,
                    LotText = $"{rowNo}행 {colNo}열",
                    MemberText = "",
                    StatusText = "WAITING",
                    TimeText = "-",
                    StatusBrush = CreateFrozenBrush("#D9D9D9")
                });
            }

            return items;
        }

        public static Brush GetInputSlotBrush(string status)
        {
            return status switch
            {
                InputSlotStatus.Loading => CreateFrozenBrush("#F4C542"),
                InputSlotStatus.Complete => CreateFrozenBrush("#D7F04A"),
                InputSlotStatus.Unloaded => CreateFrozenBrush("#6FA8DC"),
                _ => CreateFrozenBrush("#D9D9D9")
            };
        }

        public static string GetInputSlotStatusText(string status)
        {
            return status switch
            {
                InputSlotStatus.Loading => "LOADING",
                InputSlotStatus.Complete => "COMPLETE",
                InputSlotStatus.Unloaded => "UNLOADED",
                _ => "WAITING"
            };
        }

        public static Brush GetStackBoardBrush(string status)
        {
            return status switch
            {
                StackBoardStatus.Laminated => CreateFrozenBrush("#D7F04A"),
                _ => CreateFrozenBrush("#D9D9D9")
            };
        }

        public static string GetStackBoardStatusText(string status)
        {
            return status switch
            {
                StackBoardStatus.Laminated => "LAMINATED",
                _ => "WAITING"
            };
        }
    }
}