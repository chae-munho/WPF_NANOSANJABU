namespace NanoSanjabu.Models
{
    public class PlcData
    {
        public bool M200_AutoRun { get; set; }
        public bool M201_AutoStop { get; set; }
        public bool M202_LoadingMode { get; set; }
        public bool M203_UnloadingMode { get; set; }

        public short D0_Error { get; set; }

        public bool M122 { get; set; }
        public bool M127 { get; set; }
        public bool M132 { get; set; }
        public bool M137 { get; set; }
        public bool M142 { get; set; }
        public bool M147 { get; set; }
        public bool M152 { get; set; }
        public bool M157 { get; set; }
        public bool M162 { get; set; }
        public bool M167 { get; set; }

        public bool M858_GlassLoaded { get; set; }
        public bool M863_NanoDone { get; set; }
        public short D10_WorkCount { get; set; }

        public bool X07_UpperTraySensor { get; set; }
        public bool X08_LowerTraySensor { get; set; }

        public bool L1_DryStartUpper { get; set; }
        public bool L2_DryStartLower { get; set; }
        public bool L3_DryEndUpper { get; set; }
        public bool L4_DryEndLower { get; set; }

        public short D20_StackInputCount { get; set; }
        public short D22_DottingCount { get; set; }
        public short D26_StackOutCount { get; set; }

        public bool M906_StackInputDone { get; set; }
        public bool M991_DotDone { get; set; }
        public bool M922_UVRun { get; set; }
        public bool M937_StackOutDone { get; set; }

        // D60 ~ D69
        public short[] PositionIndex { get; set; } = new short[10];

        // D100, D102, ... D118
        public short[] PositionMM { get; set; } = new short[10];
    }
}