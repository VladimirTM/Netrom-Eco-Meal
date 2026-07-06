using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

[ApiController]
public class BusinessController(IBusinessService businessService) : ControllerBase
{
    public async Task<ActionResult<List<Business>>> GetAllAsync()
    {
        return await businessService.GetAllAsync();
    }
    
    public async Task<ActionResult> AddAsync(Business business)
    {
        await businessService.AddAsync(business);
        return Created();
    }
}