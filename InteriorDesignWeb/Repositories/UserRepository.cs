using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;

using InteriorDesignWeb.Repositories;
using System.Threading.Tasks;

namespace InteriorDesignWeb.Repositories
{

        public class UserRepository : IUserRepository
        {
            //private readonly DesignHubContext _context;

            //public UserRepository(DesignHubContext context)
            //{
            //    _context = context;
            //}

            //public async Task AddUserAsync(User user)
            //{
            //    await _context.Users.AddAsync(user);
            //    await _context.SaveChangesAsync();
            //}

            //public async Task UpdateUserAsync(User user)
            //{
            //    _context.Users.Update(user);
            //    await _context.SaveChangesAsync();
            //}

            //public async Task DeleteUserAsync(int userId)
            //{
            //    var user = await _context.Users.FindAsync(userId);
            //    if (user != null)
            //    {
            //        _context.Users.Remove(user);
            //        await _context.SaveChangesAsync();
            //    }
            //}

            //public async Task GetUserByUsernameAsync(string username)
            //{
            //    return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            //}

            //public async Task GetUserByPhoneNumberAsync(string phoneNumber)
            //{
            //    return await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            //}

            //public async Task UserExistsAsync(string username)
            //{
            //    return await _context.Users.AnyAsync(u => u.Username == username);
            //}

            //public async Task PhoneNumberExistsAsync(string phoneNumber)
            //{
            //    return await _context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber);
            //}
        }
    
}
