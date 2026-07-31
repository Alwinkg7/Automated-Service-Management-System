namespace ServiceApp.Core.Common
{
    // Generic version
    public class Result<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public string? ErrorMessage { get; private set; }

        private Result() { }

        public static Result<T> Success(T data) =>
            new() { IsSuccess = true, Data = data };

        public static Result<T> Failure(string errorMessage) =>
            new() { IsSuccess = false, ErrorMessage = errorMessage };
    }

    // Non-generic version — renamed Error → ErrorMessage to match above
    public class Result
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }   // ← was: Error

        private Result() { }

        public static Result Success() =>
            new() { IsSuccess = true };

        public static Result Failure(string errorMessage) =>          // ← was: error
            new() { IsSuccess = false, ErrorMessage = errorMessage }; // ← was: Error
    }
}