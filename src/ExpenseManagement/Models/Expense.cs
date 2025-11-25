namespace ExpenseManagement.Models;

public class Expense
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public int AmountMinor { get; set; }
    public decimal Amount => AmountMinor / 100.0m;
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExpenseCreateRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
}

public class ExpenseCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ExpenseStatus
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string? RoleName { get; set; }
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; }
}

public class DashboardStats
{
    public int TotalExpenses { get; set; }
    public int PendingApprovals { get; set; }
    public decimal ApprovedAmount { get; set; }
    public int ApprovedCount { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? ErrorSource { get; set; }
}
