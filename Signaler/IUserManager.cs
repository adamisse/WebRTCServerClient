using Signaler.Models;

namespace Signaler
{
    public interface IUserManager
    {
        User Create(string connectionId, string username);
        bool Delete(string id);
        IEnumerable<User> GetAll();
    }
}