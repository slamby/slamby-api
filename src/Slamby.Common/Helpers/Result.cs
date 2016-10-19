using System;

namespace Slamby.Common.Helpers
{
    public class Result
    {
        public bool IsSuccess { get; }
        public string Error { get; }
        public bool IsFailure => !IsSuccess;

        protected Result(bool isSuccess, string error)
        {
            if (isSuccess && error != string.Empty)
            {
                throw new InvalidOperationException();
            }
            if (!isSuccess && error == string.Empty)
            {
                throw new InvalidOperationException();
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Fail(string message)
        {
            return new Result(false, message);
        }

        public static Result<TValue> Fail<TValue>(string message)
        {
            return new Result<TValue>(default(TValue), false, message);
        }

        public static Result Ok()
        {
            return new Result(true, string.Empty);
        }

        public static Result<TValue> Ok<TValue>(TValue value)
        {
            return new Result<TValue>(value, true, string.Empty);
        }

        public static Result Combine(params Result[] results)
        {
            foreach (Result result in results)
            {
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Ok();
        }

        public static Result Combine(params Func<Result>[] actionResults)
        {
            foreach (Func<Result> actionResult in actionResults)
            {
                var result = actionResult.Invoke();
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Ok();
        }
    }


    public class Result<TValue> : Result
    {
        private readonly TValue _value;
        public TValue Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException();
                }

                return _value;
            }
        }

        protected internal Result(TValue value, bool isSuccess, string error)
            : base(isSuccess, error)
        {
            _value = value;
        }
    }
}