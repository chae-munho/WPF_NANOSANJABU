using System.Threading.Tasks;

namespace NanoSanjabu.Services
{
    public static class AppServices
    {
        public static DatabaseService DatabaseService { get; private set; } = null!;
        public static PlcService? PlcService { get; private set; }
        public static MesRepository MesRepository { get; private set; } = null!;
        public static MesRuntimeService MesRuntimeService { get; private set; } = null!;

        public static Task InitializeAsync()
        {
            DatabaseService = new DatabaseService();
            MesRepository = new MesRepository(DatabaseService);

            // 생성자 1개 인자만 받음
            MesRuntimeService = new MesRuntimeService(MesRepository);

            return Task.CompletedTask;
        }

        public static void AttachPlcService(PlcService plcService)
        {
            PlcService = plcService;
            MesRuntimeService.SetPlcService(plcService);
        }
    }
}