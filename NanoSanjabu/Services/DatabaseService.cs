using MySqlConnector;
using System.Data;
using System.Threading.Tasks;

namespace NanoSanjabu.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString =
            "Server=127.0.0.1;Port=3306;Database=mes_db;User ID=root;Password=1234;Charset=utf8mb4;";

        public async Task<bool> TestConnectionAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }

        public async Task<int> ExecuteScalarTestAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            const string query = "SELECT 1";
            await using var command = new MySqlCommand(query, connection);

            var result = await command.ExecuteScalarAsync();
            return System.Convert.ToInt32(result);
        }
    }
}