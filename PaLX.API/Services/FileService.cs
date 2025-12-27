using Npgsql;
using PaLX.API.Models;

namespace PaLX.API.Services
{
    public interface IFileService
    {
        Task<int> CreateFileTransferAsync(FileTransfer transfer);
        Task<FileTransfer?> GetFileTransferAsync(int id);
        Task UpdateFileStatusAsync(int id, int status);
    }

    public class FileService : IFileService
    {
        private readonly string _connectionString;

        public FileService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<int> CreateFileTransferAsync(FileTransfer transfer)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""FileTransfers"" 
                (""SenderUsername"", ""ReceiverUsername"", ""FileName"", ""FilePath"", ""FileSize"", ""ContentType"", ""SentAt"", ""Status"")
                VALUES (@s, @r, @fn, @fp, @fs, @ct, @sa, @st)
                RETURNING ""Id""";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("s", transfer.SenderUsername);
            cmd.Parameters.AddWithValue("r", transfer.ReceiverUsername);
            cmd.Parameters.AddWithValue("fn", transfer.FileName);
            cmd.Parameters.AddWithValue("fp", transfer.FilePath);
            cmd.Parameters.AddWithValue("fs", transfer.FileSize);
            cmd.Parameters.AddWithValue("ct", (object?)transfer.ContentType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sa", transfer.SentAt);
            cmd.Parameters.AddWithValue("st", transfer.Status);

            return (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        public async Task<FileTransfer?> GetFileTransferAsync(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT * FROM ""FileTransfers"" WHERE ""Id"" = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new FileTransfer
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    SenderUsername = reader.GetString(reader.GetOrdinal("SenderUsername")),
                    ReceiverUsername = reader.GetString(reader.GetOrdinal("ReceiverUsername")),
                    FileName = reader.GetString(reader.GetOrdinal("FileName")),
                    FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
                    FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
                    ContentType = reader.IsDBNull(reader.GetOrdinal("ContentType")) ? null : reader.GetString(reader.GetOrdinal("ContentType")),
                    SentAt = reader.GetDateTime(reader.GetOrdinal("SentAt")),
                    Status = reader.GetInt32(reader.GetOrdinal("Status"))
                };
            }
            return null;
        }

        public async Task UpdateFileStatusAsync(int id, int status)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"UPDATE ""FileTransfers"" SET ""Status"" = @s WHERE ""Id"" = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("s", status);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
