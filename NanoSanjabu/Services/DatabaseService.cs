using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace NanoSanjabu.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");

            var config = new ConfigurationBuilder()
                .AddJsonFile(settingsPath, optional: false, reloadOnChange: false)
                .Build();

            string host = config["DatabaseSettings:Host"] ?? "";
            string port = config["DatabaseSettings:Port"] ?? "3306";
            string database = config["DatabaseSettings:Database"] ?? "";
            string user = config["DatabaseSettings:User"] ?? "";
            string password = config["DatabaseSettings:Password"] ?? "";

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(database) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new Exception(
                    $"설정 누락\nHOST={host}\nPORT={port}\nDB={database}\nUSER={user}\nPASSWORD_EMPTY={string.IsNullOrWhiteSpace(password)}");
            }

            _connectionString =
                $"Server={host};Port={port};Database={database};User ID={user};Password={password};Charset=utf8mb4;";
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

            const string query = "SELECT 1";
            await using var command = new MySqlCommand(query, connection);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }
}