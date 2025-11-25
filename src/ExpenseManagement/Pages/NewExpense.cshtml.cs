using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class NewExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? ErrorSource { get; set; }

    public NewExpenseModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();
        Users = await _expenseService.GetUsersAsync();

        if (ExpenseService.UseDummyData)
        {
            ErrorMessage = ExpenseService.LastError;
            ErrorSource = ExpenseService.LastErrorSource;
        }
    }

    public async Task<IActionResult> OnPostAsync(int userId, int categoryId, decimal amount, DateTime expenseDate, string? description)
    {
        var request = new ExpenseCreateRequest
        {
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            ExpenseDate = expenseDate,
            Description = description
        };

        await _expenseService.CreateExpenseAsync(request);
        return RedirectToPage("/Expenses");
    }
}
