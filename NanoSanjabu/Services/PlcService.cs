using ActUtlTypeLib;
using NanoSanjabu.Models;
using System;

namespace NanoSanjabu.Services
{
    public class PlcService : IDisposable
    {
        private readonly ActUtlType _plc;
        private bool _isConnected;

        private const int LogicalStationNumber = 0;

        public PlcService()
        {
            _plc = new ActUtlType
            {
                ActLogicalStationNumber = LogicalStationNumber
            };
        }

        public bool Connect(out int errorCode)
        {
            if (_isConnected)
            {
                errorCode = 0;
                return true;
            }

            _plc.ActLogicalStationNumber = LogicalStationNumber;
            errorCode = _plc.Open();
            _isConnected = errorCode == 0;
            return _isConnected;
        }

        public bool Connect()
        {
            return Connect(out _);
        }

        public void Disconnect()
        {
            if (!_isConnected)
            {
                return;
            }

            try
            {
                _plc.Close();
            }
            finally
            {
                _isConnected = false;
            }
        }

        public bool IsConnected => _isConnected;

        public bool ReadBit(string address)
        {
            EnsureConnected();

            int value;
            int ret = _plc.GetDevice(address, out value);
            if (ret != 0)
            {
                throw new PlcException($"GetDevice 실패: {address}", ret);
            }

            return value != 0;
        }

        public short ReadWord(string address)
        {
            EnsureConnected();

            int value;
            int ret = _plc.GetDevice(address, out value);
            if (ret != 0)
            {
                throw new PlcException($"GetDevice 실패: {address}", ret);
            }

            return unchecked((short)value);
        }

        public PlcData ReadAll()
        {
            EnsureConnected();

            var data = new PlcData
            {
                M200_AutoRun = ReadBit("M200"),
                M201_AutoStop = ReadBit("M201"),
                D0_Error = ReadWord("D0"),

                M122 = ReadBit("M122"),
                M127 = ReadBit("M127"),
                M132 = ReadBit("M132"),
                M137 = ReadBit("M137"),
                M142 = ReadBit("M142"),
                M147 = ReadBit("M147"),
                M152 = ReadBit("M152"),
                M157 = ReadBit("M157"),
                M162 = ReadBit("M162"),
                M167 = ReadBit("M167"),

                M858_GlassLoaded = ReadBit("M858"),
                M863_NanoDone = ReadBit("M863"),
                D10_WorkCount = ReadWord("D10"),

                X07_UpperTray = ReadBit("X7"),
                X08_LowerTray = ReadBit("X8"),
                L1_DryStartUpper = ReadBit("L1"),
                L2_DryStartLower = ReadBit("L2"),
                L3_DryEndUpper = ReadBit("L3"),
                L4_DryEndLower = ReadBit("L4"),

                D20_StackInput = ReadWord("D20"),
                D22_DottingCount = ReadWord("D22"),
                D26_StackOutCount = ReadWord("D26"),

                M906_StackDone = ReadBit("M906"),
                M991_DotDone = ReadBit("M991"),
                M922_UVRun = ReadBit("M922"),
                M937_StackOut = ReadBit("M937")
            };

            for (int i = 0; i < 10; i++)
            {
                data.PositionIndex[i] = ReadWord($"D{60 + i}");
            }

            int[] mmAddresses = { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118 };
            for (int i = 0; i < 10; i++)
            {
                data.PositionMM[i] = ReadWord($"D{mmAddresses[i]}");
            }

            return data;
        }

        private void EnsureConnected()
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("PLC가 연결되어 있지 않습니다.");
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}