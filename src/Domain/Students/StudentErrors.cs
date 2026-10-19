using SharedKernel;

namespace Domain.Students;

public static class StudentErrors
{
    public static Error NotFound(int studentId) => Error.NotFound(
        "Students.NotFound",
        $"The student with the Id = '{studentId}' was not found");
}
