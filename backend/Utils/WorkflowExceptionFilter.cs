using HrETracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
namespace HrETracker.Utils;
public class WorkflowExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            ArgumentException => 400,
            WorkflowConflictException or EmployeeConflictException or DbUpdateConcurrencyException => 409,
            Microsoft.Data.SqlClient.SqlException { Number: 51000 or 1205 } => 409,
            DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } } => 409,
            _ => 0
        };
        if (status == 0) return;
        var title = status == 400 || context.Exception is WorkflowConflictException or EmployeeConflictException ? context.Exception.Message : "The record changed or the workflow is busy. Refresh and retry.";
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = title }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
