using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> PendingExpenses { get; set; } = new();
    public string? Filter { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorSource { get; set; }

    public ApprovalsModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync(string? filter = null)
    {
        Filter = filter;
        PendingExpenses = await _expenseService.GetPendingApprovalsAsync();
        
        if (!string.IsNullOrEmpty(filter))
        {
            PendingExpenses = PendingExpenses
                .Where(e => e.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
                           e.UserName?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
                           e.CategoryName?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        if (ExpenseService.UseDummyData)
        {
            ErrorMessage = ExpenseService.LastError;
            ErrorSource = ExpenseService.LastErrorSource;
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        // Using manager ID 2 (Bob Manager) as default reviewer
        await _expenseService.ApproveExpenseAsync(id, 2);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        await _expenseService.RejectExpenseAsync(id, 2);
        return RedirectToPage();
    }
}
