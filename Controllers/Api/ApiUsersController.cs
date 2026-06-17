using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers.Api;

[Route("api/users")]
public class ApiUsersController : ApiBaseController
{
    private readonly IUserService _users;

    public ApiUsersController(IUserService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        if (IsAdmin)
        {
            var users = await _users.GetAllAsync(CurrentUserId);
            return Ok(users);
        }
        else
        {
            var visible = await _users.GetVisibleEmployeesAsync(CurrentUserId, CurrentRole);
            var result = visible.Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                roleName = u.Role.Name,
                u.ManagerId
            });
            return Ok(result);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserViewModel model)
    {
        if (!IsAdmin)
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (success, error) = await _users.CreateAsync(model);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Tạo tài khoản người dùng thành công." });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] EditUserViewModel model)
    {
        if (!IsAdmin)
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        model.Id = id;
        var (success, error) = await _users.UpdateAsync(model, CurrentUserId);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Cập nhật tài khoản người dùng thành công." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin)
            return Forbid();

        var (success, error) = await _users.DeleteAsync(id, CurrentUserId);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Xóa người dùng thành công." });
    }
}
