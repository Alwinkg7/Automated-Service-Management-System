// =================================================================
//  Result.cs — ServiceApp.Core/Common
//
//  The Result pattern replaces throwing exceptions for expected
//  business failures.
//
//  WHY NOT JUST THROW EXCEPTIONS?
//  Exceptions = unexpected crashes (bugs, DB down, null ref).
//  Business failures = expected outcomes (no technician available,
//  request already assigned, invalid status transition).
//  Mixing them makes code messy and controllers full of try/catch.
//
//  WITH RESULT PATTERN:
//
//  Service layer:
//    if (technician == null)
//        return Result<ServiceRequest>.Failure("No technician available");
//    return Result<ServiceRequest>.Success(request);
//
//  Controller:
//    var result = await _service.AssignAsync(...);
//    if (!result.IsSuccess) {
//        TempData["Error"] = result.Error;
//        return RedirectToAction("Index");
//    }
//    return View(result.Data);
//
//  Clean, readable, no try/catch noise in controllers.
// =================================================================

namespace ServiceApp.Core.Common
{
    // Generic version — use when operation returns data on success
    // Examples: Result<ServiceRequest>, Result<Bill>
    public class Result<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }       // set on success
        public string? Error { get; private set; }  // set on failure

        // Private constructor forces use of factory methods below
        private Result() { }

        public static Result<T> Success(T data) =>
            new() { IsSuccess = true, Data = data };

        public static Result<T> Failure(string error) =>
            new() { IsSuccess = false, Error = error };
    }

    // Non-generic version — use when operation has no return data
    // Examples: Result from ToggleStatus, CancelRequest
    public class Result
    {
        public bool IsSuccess { get; private set; }
        public string? Error { get; private set; }

        private Result() { }

        public static Result Success() =>
            new() { IsSuccess = true };

        public static Result Failure(string error) =>
            new() { IsSuccess = false, Error = error };
    }
}