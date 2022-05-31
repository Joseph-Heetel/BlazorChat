using CustomBlazorApp.Server.Models;
using Microsoft.Azure.Cosmos.Scripts;

namespace CustomBlazorApp.Server.Services.DatabaseWrapper
{
    public struct TableActionResult
    {
        public enum ECode
        {
            NotInitialized,
            Success,
            NotFound,
            Error
        }

        public ECode Code;
        public bool IsSuccess { get => Code == ECode.Success; }
        public TableActionResult() { Code = ECode.NotInitialized; }
        private TableActionResult(ECode code) { Code = code; }

        public static readonly TableActionResult Success = new TableActionResult(ECode.Success);
        public static readonly TableActionResult NotFound = new TableActionResult(ECode.NotFound);
        public static readonly TableActionResult Error = new TableActionResult(ECode.Error);
    }
    public struct TableActionResult<T>
    {
        public TableActionResult.ECode Code;
        public bool IsSuccess { get => Code == TableActionResult.ECode.Success; }

        public T? Result;
        public T ResultAsserted
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException();
                }
                return Result!;
            }
        }

        public TableActionResult() { Code = TableActionResult.ECode.NotInitialized; Result = default; }
        private TableActionResult(TableActionResult.ECode code) { Code = code; Result = default; }
        private TableActionResult(T? result) { Code = TableActionResult.ECode.Success; Result = result; }

        public static TableActionResult<T> Success(T? result)
        {
            return new TableActionResult<T>(result);
        }
        public static readonly TableActionResult<T> NotFound = new TableActionResult<T>(TableActionResult.ECode.NotFound);
        public static readonly TableActionResult<T> Error = new TableActionResult<T>(TableActionResult.ECode.Error);
    }

    public interface ITableService<T>
    {
        public string PartitionPath { get; }

        public Task<TableActionResult> CreateItemAsync(T item);
        public Task<TableActionResult> ReplaceItemAsync(T item);
        public Task<TableActionResult<T>> GetItemAsync(string id, string? partitionKey = null);
        public Task<TableActionResult<List<T>>> QueryItemsAsync(string? query = default, string? partitionKey = default);
        public Task<TableActionResult> DeleteItemAsync(string id, string? partitionKey = null);
        public Task<TableActionResult> BulkDeleteItemsAsync(string? query = default, string? partitionKey = default);
    }
}
