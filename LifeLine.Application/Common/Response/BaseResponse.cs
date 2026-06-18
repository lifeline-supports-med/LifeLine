using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.Response
{
    public class BaseResponse<T>
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public int? StatusCode { get; set; }

        public static BaseResponse<T> Success(T data, string message = "Request successful", int statusCode = 200)
        {
            return new BaseResponse<T>
            {
                Data = data,
                IsSuccess = true,
                Message = message,
                StatusCode = statusCode
            };
        }

        public static BaseResponse<T> Failure(string message, string? errorCode = null, int statusCode = 400)
        {
            return new BaseResponse<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = statusCode
            };
        }

        public static BaseResponse<T> ValidationFailure(List<string> errors, int statusCode = 400)
        {
            return new BaseResponse<T>
            {
                IsSuccess = false,
                Message = "Validation failed",
                Errors = errors,
                StatusCode = statusCode
            };
        }

        public static BaseResponse<T> Unauthorized(string message = "Authentication failed", int statusCode = 401)
        {
            return new BaseResponse<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = statusCode
            };
        }
    }
}
