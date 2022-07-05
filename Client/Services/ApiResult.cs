using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BlazorChat.Client.Services
{
    /// <summary>
    /// Statuscode detailing an api response
    /// </summary>
    public enum EApiStatusCode
    {
        /// <summary>
        /// The api action was performed as expected
        /// </summary>
        Success,
        /// <summary>
        /// The api action was aborted because a precondition was not met
        /// </summary>
        PreconditionFailed,
        /// <summary>
        /// The api action resulted in a non-success http status code
        /// </summary>
        NetErrorCode,
        /// <summary>
        /// The api action failed because the http send method threw an exception
        /// </summary>
        NetException,
        /// <summary>
        /// The api action failed because json (de-)serialization threw an exception
        /// </summary>
        JsonException
    }

    /// <summary>
    /// Result type describing a non-value outcome of an api action
    /// </summary>
    public struct ApiResult
    {
        /// <summary>
        /// Api status code determines success or error information
        /// </summary>
        public EApiStatusCode StatusCode { get; set; } = default;
        /// <summary>
        /// Http status code of the underlying api request
        /// </summary>
        public HttpStatusCode NetStatusCode { get; set; } = default;
        /// <summary>
        /// True if api action was successful
        /// </summary>
        public bool IsSuccess => StatusCode == EApiStatusCode.Success;
        public ApiResult() { }
        public ApiResult(EApiStatusCode statusCode, HttpStatusCode netStatusCode)
        {
            StatusCode = statusCode;
            NetStatusCode = netStatusCode;
        }
        public static implicit operator bool(ApiResult v) { return v.IsSuccess; }
        public static readonly ApiResult NetException = new ApiResult(EApiStatusCode.NetException, default);
        public static readonly ApiResult JsonException = new ApiResult(EApiStatusCode.JsonException, default);
        public static readonly ApiResult PreconditionFail = new ApiResult(EApiStatusCode.PreconditionFailed, default);
    }

    /// <summary>
    /// Result type describing a value outcome of an api action
    /// </summary>
    public struct ApiResult<T>
    {
        /// <summary>
        /// Api status code determines success or error information
        /// </summary>
        public EApiStatusCode StatusCode { get; set; } = default;
        /// <summary>
        /// Http status code of the underlying api request
        /// </summary>
        public HttpStatusCode NetStatusCode { get; set; } = default;
        /// <summary>
        /// The result of the outcome. Will contain a value if successful
        /// </summary>
        public T? Result { get; set; } = default;
        /// <summary>
        /// True if api action was successful
        /// </summary>
        public bool IsSuccess => StatusCode == EApiStatusCode.Success;
        /// <summary>
        /// Access the result in non-nullable style if success is known
        /// </summary>
        public T ResultAsserted => IsSuccess ? Result! : throw new InvalidOperationException();
        /// <summary>
        /// Access the result if action was successful
        /// </summary>
        /// <param name="result">Result</param>
        /// <returns>True, if action was successful</returns>
        public bool TryGet(out T result)
        {
            result = Result!;
            return IsSuccess;
        }

        public static implicit operator bool(ApiResult<T> v) { return v.IsSuccess; }
        public static implicit operator T?(ApiResult<T> v) { return v.Result; }
        public static implicit operator ApiResult(ApiResult<T> v) { return new ApiResult(v.StatusCode, v.NetStatusCode); }

        public ApiResult() { }

        public ApiResult(EApiStatusCode statusCode, HttpStatusCode netStatusCode, T? result)
        {
            StatusCode = statusCode;
            NetStatusCode = netStatusCode;
            Result = result;
        }

        public static readonly ApiResult<T> NetException = new ApiResult<T>(EApiStatusCode.NetException, default, default);
        public static readonly ApiResult<T> JsonException = new ApiResult<T>(EApiStatusCode.JsonException, default, default);
        public static readonly ApiResult<T> PreconditionFail = new ApiResult<T>(EApiStatusCode.PreconditionFailed, default, default);
    }
}
