using MySqlConnector;
using System.Data;
using System.Threading.Tasks;

namespace NanoSanjabu.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString =
            "Server=localhost;Port=3306;Database=testdb;Uid=root;Pwd=1234;Charset=utf8mb4;";

        public string ConnectionString => _connectionString;

        public async Task<MySqlConnection> CreateOpenConnectionAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

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

            const string sql = "SELECT 1";
            await using var command = new MySqlCommand(sql, connection);
            var result = await command.ExecuteScalarAsync();
            return System.Convert.ToInt32(result);
        }
    }
}