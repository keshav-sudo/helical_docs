using Microsoft.AspNetCore.Mvc;
using SysproIntegrationApi.Models;
using SysproIntegrationApi.Services;

namespace SysproIntegrationApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        CustomerService customerService,
        ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    /// <summary>
    /// Get customer by code
    /// </summary>
    [HttpGet("{customerCode}")]
    public async Task<ActionResult<Customer>> GetCustomer(
        string customerCode, 
        CancellationToken ct)
    {
        var customer = await _customerService.GetCustomerAsync(customerCode, ct);
        
        if (customer == null)
            return NotFound(new { error = $"Customer '{customerCode}' not found" });
        
        return Ok(customer);
    }

    /// <summary>
    /// Search customers
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Customer>>> SearchCustomers(
        [FromQuery] string? name,
        [FromQuery] int maxRecords = 100,
        CancellationToken ct = default)
    {
        var customers = await _customerService.SearchCustomersAsync(name, maxRecords, ct);
        return Ok(customers);
    }

    /// <summary>
    /// Create new customer
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateCustomerResult>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.CustomerCode))
            return BadRequest(new { error = "CustomerCode is required" });
        
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest(new { error = "Name is required" });

        try
        {
            var result = await _customerService.CreateCustomerAsync(request, ct);
            return CreatedAtAction(
                nameof(GetCustomer), 
                new { customerCode = result.CustomerCode }, 
                result);
        }
        catch (SysproException ex)
        {
            _logger.LogError(ex, "Failed to create customer");
            return BadRequest(new { error = ex.Message });
        }
    }
}
